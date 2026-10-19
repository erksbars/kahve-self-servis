using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kahve.Api.Tests;

public sealed class IsolationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private HttpClient Client() => fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
    private static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }
    private static async Task<HttpResponseMessage> Mutate(HttpClient client, HttpMethod method, string path, object body)
    {
        var csrf = await Read(await client.GetAsync("/api/session/csrf"));
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
    private static async Task Login(HttpClient client, string email)
    {
        (await Mutate(client, HttpMethod.Post, "/api/session/login", new { email, password = ApiFixture.Password })).EnsureSuccessStatusCode();
    }
    [Fact]
    public async Task Public_menus_use_branch_prices_and_distinct_tenant_products()
    {
        using var c = Client();
        var a = await Read(await c.GetAsync("/api/branches/demo-moda/menu"));
        var b = await Read(await c.GetAsync("/api/branches/demo-besiktas/menu"));
        var second = await Read(await c.GetAsync("/api/branches/demo-moda-2/menu"));
        Assert.Equal(140m, a.GetProperty("products")[0].GetProperty("price").GetDecimal());
        Assert.Equal(155m, b.GetProperty("products")[0].GetProperty("price").GetDecimal());
        Assert.Equal(150m, second.GetProperty("products")[0].GetProperty("price").GetDecimal());
        Assert.NotEqual(a.GetProperty("products")[0].GetProperty("id").GetString(), b.GetProperty("products")[0].GetProperty("id").GetString());
        Assert.True(a.GetProperty("demo").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/branches/unknown/menu")).StatusCode);
    }
    [Fact]
    public async Task Staff_endpoints_require_authentication()
    {
        using var c = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/staff/branches")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/admin/products")).StatusCode);
    }
    [Fact]
    public async Task Login_requires_csrf_and_rejects_wrong_credentials()
    {
        using var c = Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/session/login", new { email = "owner-a@demo.local", password = ApiFixture.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Mutate(c, HttpMethod.Post, "/api/session/login", new { email = "owner-a@demo.local", password = "wrong" })).StatusCode);
    }
    [Fact]
    public async Task Owner_cannot_read_or_mutate_another_tenant()
    {
        using var c = Client();
        var foreignMenu = await Read(await c.GetAsync("/api/branches/demo-besiktas/menu"));
        await Login(c, "owner-a@demo.local");
        var branches = await Read(await c.GetAsync("/api/staff/branches"));
        Assert.Equal(2, branches.GetArrayLength());
        Assert.All(branches.EnumerateArray(), x => Assert.StartsWith("demo-moda", x.GetProperty("slug").GetString()));
        var products = await Read(await c.GetAsync("/api/admin/products"));
        var foreignIds = foreignMenu.GetProperty("products").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToHashSet();
        Assert.Equal(24, products.GetArrayLength());
        Assert.All(products.EnumerateArray(), x => Assert.DoesNotContain(x.GetProperty("id").GetString(), foreignIds));
        var id = foreignMenu.GetProperty("branch").GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await Mutate(c, HttpMethod.Patch, $"/api/staff/branches/{id}/accepting-orders", new { isAcceptingOrders = false, expectedVersion = 1 })).StatusCode);
    }
    [Fact]
    public async Task Barista_has_branch_scope_and_cannot_use_owner_endpoint()
    {
        using var c = Client();
        var second = await Read(await c.GetAsync("/api/branches/demo-moda-2/menu"));
        await Login(c, "barista-a@demo.local");
        var branches = await Read(await c.GetAsync("/api/staff/branches"));
        Assert.Single(branches.EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/products")).StatusCode);
        var id = second.GetProperty("branch").GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await Mutate(c, HttpMethod.Patch, $"/api/staff/branches/{id}/accepting-orders", new { isAcceptingOrders = false, expectedVersion = 1 })).StatusCode);
    }
    [Fact]
    public async Task Branch_update_checks_version_persists_audit_and_is_visible_publicly()
    {
        using var c = Client();
        await Login(c, "owner-a@demo.local");
        var branches = await Read(await c.GetAsync("/api/staff/branches"));
        var branch = branches.EnumerateArray().Single(x => x.GetProperty("slug").GetString() == "demo-moda");
        var id = branch.GetProperty("id").GetGuid();
        var version = branch.GetProperty("version").GetInt32();
        var path = $"/api/staff/branches/{id}/accepting-orders";
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.AuditLogs.IgnoreQueryFilters().CountAsync(x => x.EntityId == id);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PatchAsync(path, JsonContent.Create(new { isAcceptingOrders = false, expectedVersion = version }))).StatusCode);
        (await Mutate(c, HttpMethod.Patch, path, new { isAcceptingOrders = false, expectedVersion = version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Mutate(c, HttpMethod.Patch, path, new { isAcceptingOrders = true, expectedVersion = version })).StatusCode);
        var menu = await Read(await c.GetAsync("/api/branches/demo-moda/menu"));
        Assert.False(menu.GetProperty("branch").GetProperty("isAcceptingOrders").GetBoolean());
        Assert.Equal(before + 1, await db.AuditLogs.IgnoreQueryFilters().CountAsync(x => x.EntityId == id));
        (await Mutate(c, HttpMethod.Patch, path, new { isAcceptingOrders = true, expectedVersion = version + 1 })).EnsureSuccessStatusCode();
    }
    [Fact]
    public async Task Logout_invalidates_browser_session()
    {
        using var c = Client();
        await Login(c, "owner-b@demo.local");
        (await Mutate(c, HttpMethod.Post, "/api/session/logout", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/session/me")).StatusCode);
    }
    [Fact]
    public async Task Database_rejects_cross_tenant_catalog_relationship()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = await db.Branches.IgnoreQueryFilters().SingleAsync(x => x.Slug == "demo-moda");
        var b = await db.Branches.IgnoreQueryFilters().SingleAsync(x => x.Slug == "demo-besiktas");
        var product = await db.Products.IgnoreQueryFilters().FirstAsync(x => x.TenantId == b.TenantId);
        db.BranchProducts.Add(new BranchProduct { TenantId = a.TenantId, BranchId = a.Id, ProductId = product.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Anonymous_db_scope_returns_no_private_entities_and_seed_is_idempotent()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.StaffUsers.ToListAsync());
        Assert.Empty(await db.Branches.ToListAsync());
        var before = await db.Products.IgnoreQueryFilters().CountAsync();
        await DemoSeed.Run(db, ApiFixture.Password);
        Assert.Equal(before, await db.Products.IgnoreQueryFilters().CountAsync());
    }
}
