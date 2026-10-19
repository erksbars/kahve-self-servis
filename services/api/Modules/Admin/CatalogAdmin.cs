using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Admin;
public sealed record CategoryInput(string Name, int SortOrder, int ExpectedVersion = 0);
public sealed record ValueInput(Guid Id, string Name, decimal PriceDelta, bool Available, int SortOrder);
public sealed record GroupInput(Guid Id, string Name, int MinSelect, int MaxSelect, int SortOrder, List<ValueInput?>? Values);
public sealed record ProductInput(Guid CategoryId, string Name, string Description, decimal BasePrice, decimal TaxRate, bool Active, int SortOrder, List<GroupInput?>? OptionGroups, int ExpectedVersion = 0);
public sealed record OverrideInput(Guid OptionValueId, bool Available, decimal? PriceOverride);
public sealed record BranchProductInput(bool Available, decimal? PriceOverride, int ExpectedVersion, List<OverrideInput>? Options);

public static partial class AdminEndpoints
{
    private static void MapCatalog(RouteGroupBuilder group)
    {
        group.MapGet("/catalog", async (AppDbContext db, CancellationToken ct) =>
        {
            var categories = await db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(ct);
            var products = await db.Products.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(ct);
            var groups = await db.OptionGroups.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(ct);
            var values = await db.OptionValues.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(ct);
            var branchProducts = await db.BranchProducts.AsNoTracking().ToListAsync(ct);
            var branchOptions = await db.BranchOptions.AsNoTracking().ToListAsync(ct);
            return Results.Ok(new { categories = categories.Select(x => new { x.Id, x.Name, x.SortOrder, x.Version }), products = products.Select(p => new { p.Id, p.CategoryId, p.Name, p.Description, p.BasePrice, p.TaxRate, p.Active, p.SortOrder, p.Version, imageUrl = p.ImagePath, p.ImageAlt, optionGroups = groups.Where(g => g.ProductId == p.Id).Select(g => new { g.Id, g.Name, g.MinSelect, g.MaxSelect, g.SortOrder, values = values.Where(v => v.OptionGroupId == g.Id).Select(v => new { v.Id, v.Name, v.PriceDelta, v.Available, v.SortOrder }) }) }), branchProducts = branchProducts.Select(x => new { x.BranchId, x.ProductId, x.PriceOverride, x.Available, x.Version }), branchOptions = branchOptions.Select(x => new { x.BranchId, x.OptionValueId, x.PriceOverride, x.Available }) });
        });
        group.MapPost("/categories", async (CategoryInput input, AppDbContext db, RequestScope scope, CancellationToken ct) =>
        {
            if (!Text(input.Name, 80) || input.SortOrder is < 0 or > 9999) return Invalid("Kategori adı ve sırası geçersiz.");
            var entity = new Category { Id = Guid.NewGuid(), TenantId = scope.TenantId!.Value, Name = input.Name.Trim(), SortOrder = input.SortOrder };
            db.Categories.Add(entity); Audit(db, scope, entity.Id, "category.create"); await db.SaveChangesAsync(ct); return Results.Ok(new { entity.Id });
        });
        group.MapPut("/categories/{id:guid}", async (Guid id, CategoryInput input, AppDbContext db, RequestScope scope, CancellationToken ct) =>
        {
            var entity = await db.Categories.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return Results.NotFound();
            if (entity.Version != input.ExpectedVersion) return Conflict();
            if (!Text(input.Name, 80) || input.SortOrder is < 0 or > 9999) return Invalid("Kategori adı ve sırası geçersiz.");
            entity.Name = input.Name.Trim(); entity.SortOrder = input.SortOrder; entity.Version++;
            Audit(db, scope, id, "category.update"); await NotifyBranches(db, ct); await db.SaveChangesAsync(ct); return Results.Ok(new { entity.Id, entity.Version });
        });
        group.MapPost("/products", (ProductInput input, AppDbContext db, RequestScope scope, CancellationToken ct) => SaveProduct(null, input, db, scope, ct));
        group.MapPut("/products/{id:guid}", (Guid id, ProductInput input, AppDbContext db, RequestScope scope, CancellationToken ct) => SaveProduct(id, input, db, scope, ct));
        group.MapPut("/branches/{branchId:guid}/products/{productId:guid}", SaveBranchProduct);
    }
    private static async Task<IResult> SaveProduct(Guid? id, ProductInput input, AppDbContext db, RequestScope scope, CancellationToken ct)
    {
        Product? product = id.HasValue ? await db.Products.SingleOrDefaultAsync(x => x.Id == id, ct) : new() { Id = Guid.NewGuid(), TenantId = scope.TenantId!.Value };
        if (product is null) return Results.NotFound();
        if (id.HasValue && product.Version != input.ExpectedVersion) return Conflict();
        if (!Text(input.Name, 120) || input.Description is null || input.Description.Length > 500 || !Price(input.BasePrice) || input.TaxRate is < 0 or > 100 || decimal.Round(input.TaxRate, 2) != input.TaxRate || input.SortOrder is < 0 or > 9999) return Invalid("Ürün adı, fiyatı, vergi oranı veya sırası geçersiz.");
        if (!await db.Categories.AnyAsync(x => x.Id == input.CategoryId, ct)) return Invalid("Geçerli bir işletme kategorisi seçin.");
        if (input.OptionGroups is null || input.OptionGroups.Count > 10 || input.OptionGroups.Any(g => g is null || !Text(g.Name, 80) || g.Values is null || g.Values.Count is < 1 or > 10 || g.MinSelect < 0 || g.MaxSelect < 1 || g.MaxSelect < g.MinSelect || g.MaxSelect > g.Values.Count || g.SortOrder is < 0 or > 9999 || g.Values.Any(v => v is null || !Text(v.Name, 80) || !Price(v.PriceDelta) || v.SortOrder is < 0 or > 9999)) || input.OptionGroups.Sum(g => g!.MaxSelect) > 10) return Invalid("Seçenek gruplarını kontrol edin; toplam en fazla 10 seçim olabilir.");
        var oldGroups = await db.OptionGroups.Where(x => x.ProductId == product.Id).ToListAsync(ct);
        var oldGroupIds = oldGroups.Select(x => x.Id).ToList();
        var oldValues = await db.OptionValues.Where(x => oldGroupIds.Contains(x.OptionGroupId)).ToListAsync(ct);
        var selectedGroups = new HashSet<Guid>(); var selectedValues = new HashSet<Guid>();
        foreach (var g in input.OptionGroups)
        {
            if (g!.Id != Guid.Empty && !oldGroups.Any(x => x.Id == g.Id)) return Invalid("Seçenek grubu bu ürüne ait değil.");
            if (g.Id != Guid.Empty && !selectedGroups.Add(g.Id)) return Invalid("Tekrar seçenek grubu gönderilemez.");
            foreach (var v in g.Values!)
            {
                if (v!.Id != Guid.Empty && (!oldValues.Any(x => x.Id == v.Id && x.OptionGroupId == g.Id) || !selectedValues.Add(v.Id))) return Invalid("Seçenek kimliği geçersiz veya tekrarlandı.");
            }
        }
        product.CategoryId = input.CategoryId; product.Name = input.Name.Trim(); product.Description = input.Description.Trim(); product.BasePrice = input.BasePrice; product.TaxRate = input.TaxRate; product.Active = input.Active; product.SortOrder = input.SortOrder;
        if (id.HasValue) product.Version++; else
        {
            db.Products.Add(product);
            foreach (var branch in await db.Branches.ToListAsync(ct)) db.BranchProducts.Add(new() { TenantId = product.TenantId, ProductId = product.Id, BranchId = branch.Id });
        }
        db.OptionGroups.RemoveRange(oldGroups.Where(x => !selectedGroups.Contains(x.Id)));
        db.OptionValues.RemoveRange(oldValues.Where(x => !selectedValues.Contains(x.Id) && selectedGroups.Contains(x.OptionGroupId)));
        foreach (var g in input.OptionGroups)
        {
            var entity = oldGroups.SingleOrDefault(x => x.Id == g!.Id) ?? new OptionGroup { Id = Guid.NewGuid(), TenantId = product.TenantId, ProductId = product.Id };
            if (g!.Id == Guid.Empty) db.OptionGroups.Add(entity);
            entity.Name = g.Name.Trim(); entity.MinSelect = g.MinSelect; entity.MaxSelect = g.MaxSelect; entity.SortOrder = g.SortOrder;
            foreach (var v in g.Values!)
            {
                var value = oldValues.SingleOrDefault(x => x.Id == v!.Id) ?? new OptionValue { Id = Guid.NewGuid(), TenantId = product.TenantId, OptionGroupId = entity.Id };
                if (v!.Id == Guid.Empty) db.OptionValues.Add(value);
                value.Name = v.Name.Trim(); value.PriceDelta = v.PriceDelta; value.Available = v.Available; value.SortOrder = v.SortOrder;
            }
        }
        Audit(db, scope, product.Id, id.HasValue ? "product.update" : "product.create"); await NotifyBranches(db, ct); await db.SaveChangesAsync(ct); return Results.Ok(new { product.Id, product.Version });
    }
    private static async Task<IResult> SaveBranchProduct(Guid branchId, Guid productId, BranchProductInput input, AppDbContext db, RequestScope scope, CancellationToken ct)
    {
        var bp = await db.BranchProducts.SingleOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == productId, ct);
        if (bp is null) return Results.NotFound();
        if (bp.Version != input.ExpectedVersion) return Conflict();
        if (input.PriceOverride.HasValue && !Price(input.PriceOverride.Value)) return Invalid("Şube fiyatı geçersiz.");
        var validIds = await (from v in db.OptionValues join g in db.OptionGroups on v.OptionGroupId equals g.Id where g.ProductId == productId select v.Id).ToListAsync(ct);
        if (input.Options is null || input.Options.Count > 100 || input.Options.Any(x => x is null) || input.Options.Select(x => x.OptionValueId).Distinct().Count() != input.Options.Count || input.Options.Any(x => !validIds.Contains(x.OptionValueId) || (x.PriceOverride.HasValue && !Price(x.PriceOverride.Value)))) return Invalid("Şube seçeneklerini kontrol edin.");
        bp.PriceOverride = input.PriceOverride; bp.Available = input.Available; bp.Version++;
        foreach (var v in input.Options)
        {
            var entity = await db.BranchOptions.SingleOrDefaultAsync(x => x.BranchId == branchId && x.OptionValueId == v.OptionValueId, ct);
            if (entity is null) { entity = new() { TenantId = scope.TenantId!.Value, BranchId = branchId, OptionValueId = v.OptionValueId }; db.BranchOptions.Add(entity); }
            entity.Available = v.Available; entity.PriceOverride = v.PriceOverride;
        }
        Audit(db, scope, productId, "branch-product.update"); await NotifyBranches(db, ct); await db.SaveChangesAsync(ct); return Results.Ok(new { bp.Version });
    }
}

