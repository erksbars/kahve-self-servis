using System.Data;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Orders;

public sealed record QuoteRequest(string? BranchSlug, List<QuoteItemRequest?>? Items);
public sealed record QuoteItemRequest(Guid ProductId, int Quantity, List<Guid>? OptionValueIds);
public sealed record QuotedOption(Guid Id, string Group, string Name, decimal PriceDelta);
public sealed record QuotedItem(Guid ProductId, string Name, int Quantity, decimal BasePrice, decimal UnitPrice, decimal LineTotal, List<QuotedOption> Options, decimal TaxRate = 0);
public sealed record QuoteDto(string BranchSlug, string Currency, decimal Total, DateTimeOffset CalculatedAt, List<QuotedItem> Items);

public static class QuoteEndpoints
{
    public static void MapQuoteEndpoints(this WebApplication app) => app.MapPost("/api/checkouts/quote", Quote).RequireRateLimiting("quote");

    private static async Task<IResult> Quote(QuoteRequest request, AppDbContext db, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var result = await Calculate(request, db, ct);
        await transaction.CommitAsync(ct);
        return result.Error ?? Results.Ok(result.Quote);
    }

    public sealed record PricingResult(QuoteDto? Quote, IResult? Error);
    public static async Task<PricingResult> Calculate(QuoteRequest request, AppDbContext db, CancellationToken ct)
    {
        static PricingResult Invalid(string message) => new(null, Results.BadRequest(new { code = "invalid_cart", message }));
        if (string.IsNullOrWhiteSpace(request.BranchSlug) || request.BranchSlug.Length > 100)
            return Invalid("Geçerli bir şube seçin.");
        if (request.Items is null || request.Items.Count is < 1 or > 20)
            return Invalid("Sepette 1–20 ürün satırı olmalı.");
        if (request.Items.Any(x => x is null || x.Quantity is < 1 or > 10 || x.OptionValueIds is null || x.OptionValueIds.Count > 10))
            return Invalid("Her satır için 1–10 adet ve geçerli seçenekler gönderin.");
        if (request.Items.Sum(x => x!.Quantity) > 50) return Invalid("Bir sepette en fazla 50 adet olabilir.");
        // One consistent catalog snapshot; no client price or total is accepted as a pricing source.
        var menu = await CatalogReader.Read(db, request.BranchSlug, false, ct);
        if (menu is null) return new(null, Results.NotFound(new { code = "branch_not_found", message = "Şube bulunamadı." }));
        if (!menu.Branch.IsAcceptingOrders) return new(null, Results.Conflict(new { code = "branch_closed", message = "Şube şu anda sipariş kabul etmiyor. Sepetiniz korunuyor." }));
        var lines = new List<QuotedItem>();
        foreach (var item in request.Items)
        {
            var product = menu.Products.SingleOrDefault(x => x.Id == item!.ProductId);
            if (product is null || !product.Available)
                return new(null, Results.Conflict(new { code = "product_unavailable", message = "Sepetteki bir ürün artık bu şubede satışta değil. Sepeti güncelleyin." }));
            var ids = item!.OptionValueIds!;
            if (ids.Distinct().Count() != ids.Count) return Invalid("Aynı seçenek birden fazla seçilemez.");
            var options = new List<QuotedOption>();
            foreach (var group in product.OptionGroups)
            {
                var selected = group.Values.Where(x => ids.Contains(x.Id)).ToList();
                if (selected.Count < group.MinSelect || selected.Count > group.MaxSelect)
                    return Invalid($"{product.Name}: {group.Name} için {group.MinSelect}–{group.MaxSelect} seçim gerekli.");
                if (selected.Any(x => !x.Available))
                    return new(null, Results.Conflict(new { code = "option_unavailable", message = $"{product.Name}: seçtiğiniz bir seçenek şu anda bulunmuyor. Seçenekleri güncelleyin." }));
                options.AddRange(selected.Select(x => new QuotedOption(x.Id, group.Name, x.Name, x.PriceDelta)));
            }
            if (options.Count != ids.Count) return Invalid("Seçenekler seçilen ürüne ve şubeye ait olmalı.");
            var unit = product.Price + options.Sum(x => x.PriceDelta);
            lines.Add(new(product.Id, product.Name, item.Quantity, product.Price, unit, unit * item.Quantity, options, product.TaxRate));
        }
        return new(new QuoteDto(menu.Branch.Slug, "TRY", lines.Sum(x => x.LineTotal), DateTimeOffset.UtcNow, lines), null);
    }
}
