using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Catalog;
using Kahve.Api.Modules.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kahve.Api.Tests;

public sealed class QuoteTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private HttpClient Client() => fixture.Factory.CreateClient();
    private static async Task<MenuDto> Menu(HttpClient c, string slug = "demo-moda") => (await c.GetFromJsonAsync<MenuDto>($"/api/branches/{slug}/menu"))!;
    private static List<Guid> Defaults(MenuProduct p) => p.OptionGroups.SelectMany(g => g.Values.Where(v => v.Available).Take(g.MinSelect).Select(v => v.Id)).ToList();
    private static async Task<HttpResponseMessage> Quote(HttpClient c, object body)
    {
        var token = await c.GetFromJsonAsync<JsonElement>("/api/session/csrf");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkouts/quote") { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        return await c.SendAsync(request);
    }
    private static object Body(MenuProduct p, List<Guid>? options = null, int quantity = 1, string slug = "demo-moda") => new { branchSlug = slug, items = new[] { new { productId = p.Id, quantity, optionValueIds = options ?? Defaults(p) } } };

    [Fact]
    public async Task Anonymous_customer_can_quote_variants_milk_extras_and_quantity()
    {
        using var c = Client();
        var p = (await Menu(c)).Products[0];
        var ids = p.OptionGroups.SelectMany(g => g.Values).Where(v => v.Name is "Büyük" or "Yulaf sütü" or "Ek espresso").Select(v => v.Id).ToList();
        var response = await Quote(c, Body(p, ids, 2));
        response.EnsureSuccessStatusCode();
        var quote = (await response.Content.ReadFromJsonAsync<QuoteDto>())!;
        Assert.Equal(430m, quote.Total);
        Assert.Equal(215m, quote.Items[0].UnitPrice);
        Assert.Equal(3, quote.Items[0].Options.Count);
        Assert.Equal("TRY", quote.Currency);
    }
    [Fact]
    public async Task Client_supplied_prices_cannot_change_server_total()
    {
        using var c = Client();
        var p = (await Menu(c)).Products[0];
        var response = await Quote(c, new { branchSlug = "demo-moda", total = 0, items = new[] { new { productId = p.Id, quantity = 2, optionValueIds = Defaults(p), unitPrice = 0 } } });
        response.EnsureSuccessStatusCode();
        Assert.Equal(280m, (await response.Content.ReadFromJsonAsync<QuoteDto>())!.Total);
    }
    [Fact]
    public async Task Foreign_products_and_foreign_options_are_rejected()
    {
        using var c = Client();
        var menu = await Menu(c);
        var foreign = (await Menu(c, "demo-besiktas")).Products[0];
        Assert.Equal(HttpStatusCode.Conflict, (await Quote(c, Body(foreign))).StatusCode);
        var p = menu.Products[0];
        Assert.Equal(HttpStatusCode.BadRequest, (await Quote(c, Body(p, [.. Defaults(p), foreign.OptionGroups[0].Values[0].Id]))).StatusCode);
        var otherProductOption = menu.Products[1].OptionGroups[0].Values[0].Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await Quote(c, Body(p, [.. Defaults(p), otherProductOption]))).StatusCode);
    }
    [Fact]
    public async Task Required_maximum_duplicate_and_cart_limits_are_enforced()
    {
        using var c = Client();
        var p = (await Menu(c)).Products[0];
        var defaults = Defaults(p);
        var extras = p.OptionGroups.Single(g => g.Name == "Ekstralar").Values.Select(v => v.Id).ToList();
        var invalid = new object[] {
            Body(p, []), Body(p, [.. defaults, defaults[0]]), Body(p, [.. defaults, .. extras]),
            Body(p, quantity: 0), Body(p, quantity: 11),
            new { branchSlug = "demo-moda", items = Array.Empty<object>() },
            new { branchSlug = "demo-moda", items = Enumerable.Repeat(new { productId = p.Id, quantity = 1, optionValueIds = defaults }, 21).ToArray() },
            new { branchSlug = "demo-moda", items = Enumerable.Repeat(new { productId = p.Id, quantity = 10, optionValueIds = defaults }, 6).ToArray() },
            new { branchSlug = "demo-moda", items = new object?[] { null } },
            new { branchSlug = "demo-moda", items = new[] { new { productId = p.Id, quantity = 1, optionValueIds = (List<Guid>?)null } } }
        };
        foreach (var body in invalid) Assert.Equal(HttpStatusCode.BadRequest, (await Quote(c, body)).StatusCode);
    }
    [Theory]
    [InlineData("branch")]
    [InlineData("product")]
    [InlineData("branch-product")]
    [InlineData("option")]
    [InlineData("branch-option")]
    public async Task Catalog_closed_after_cart_creation_is_rejected(string target)
    {
        using var c = Client();
        var menu = await Menu(c);
        var p = menu.Products[0];
        var defaults = Defaults(p);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(x => x.Id == menu.Branch.Id);
        var product = await db.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == p.Id);
        var bp = await db.BranchProducts.IgnoreQueryFilters().SingleAsync(x => x.BranchId == branch.Id && x.ProductId == p.Id);
        var option = await db.OptionValues.IgnoreQueryFilters().SingleAsync(x => x.Id == defaults[0]);
        BranchOption? branchOption = null;
        switch (target)
        {
            case "branch": branch.IsAcceptingOrders = false; break;
            case "product": product.Active = false; break;
            case "branch-product": bp.Available = false; break;
            case "option": option.Available = false; break;
            case "branch-option": branchOption = new() { TenantId = branch.TenantId, BranchId = branch.Id, OptionValueId = option.Id, Available = false }; db.BranchOptions.Add(branchOption); break;
        }
        await db.SaveChangesAsync();
        try { Assert.Equal(HttpStatusCode.Conflict, (await Quote(c, Body(p, defaults))).StatusCode); }
        finally
        {
            branch.IsAcceptingOrders = true; product.Active = true; bp.Available = true; option.Available = true;
            if (branchOption is not null) db.BranchOptions.Remove(branchOption);
            await db.SaveChangesAsync();
        }
    }
    [Fact]
    public async Task Quote_uses_current_branch_option_price_and_previous_response_stays_unchanged()
    {
        using var c = Client();
        var menu = await Menu(c);
        var p = menu.Products[0];
        var original = (await (await Quote(c, Body(p))).Content.ReadFromJsonAsync<QuoteDto>())!;
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(x => x.Id == menu.Branch.Id);
        var change = new BranchOption { TenantId = branch.TenantId, BranchId = branch.Id, OptionValueId = Defaults(p)[0], PriceOverride = 7m };
        db.BranchOptions.Add(change);
        await db.SaveChangesAsync();
        try
        {
            var updated = (await (await Quote(c, Body(p))).Content.ReadFromJsonAsync<QuoteDto>())!;
            Assert.Equal(147m, updated.Total);
            Assert.Equal(140m, original.Total);
            Assert.Equal(7m, (await Menu(c)).Products[0].OptionGroups[0].Values[0].PriceDelta);
        }
        finally { db.BranchOptions.Remove(change); await db.SaveChangesAsync(); }
    }
    [Fact]
    public async Task Database_rejects_cross_tenant_option_group()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantA = await db.Tenants.IgnoreQueryFilters().SingleAsync(x => x.Name == "Demo Kahve A");
        var productB = await db.Products.IgnoreQueryFilters().FirstAsync(x => x.TenantId != tenantA.Id);
        db.OptionGroups.Add(new() { Id = Guid.NewGuid(), TenantId = tenantA.Id, ProductId = productB.Id, Name = "Invalid", MaxSelect = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
