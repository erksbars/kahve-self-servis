using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Admin;
using Kahve.Api.Modules.Catalog;
using Kahve.Api.Modules.Orders;
using Kahve.Api.Modules.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kahve.Api.Tests;
public sealed class AdminTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private HttpClient Client() => fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
    private static async Task<JsonElement> Read(HttpResponseMessage response) { response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone(); }
    private static async Task<HttpResponseMessage> Send(HttpClient c, HttpMethod method, string path, object body)
    {
        var csrf = await c.GetFromJsonAsync<JsonElement>("/api/session/csrf");
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await c.SendAsync(request);
    }
    private static async Task Login(HttpClient c, string email = "owner-a@demo.local", string password = ApiFixture.Password) => (await Send(c, HttpMethod.Post, "/api/session/login", new { email, password })).EnsureSuccessStatusCode();
    private static async Task<Guid> Category(HttpClient c) => (await Read(await c.GetAsync("/api/admin/catalog"))).GetProperty("categories")[0].GetProperty("id").GetGuid();
    private static ProductInput Product(Guid category, bool options = false) => new(category, "Test " + Guid.NewGuid().ToString("N"), "Test açıklaması", 100, 10, true, 9000, options ? [new(Guid.Empty, "Ekstra", 0, 1, 0, [new(Guid.Empty, "Süt", 20, true, 0)])] : []);
    private static async Task<Guid> CreateProduct(HttpClient c, ProductInput input) => (await Read(await Send(c, HttpMethod.Post, "/api/admin/products", input))).GetProperty("id").GetGuid();
    private static async Task<Guid> CreateBranch(HttpClient c, List<OpeningWindow>? hours = null) => (await Read(await Send(c, HttpMethod.Post, "/api/admin/branches", new BranchInput("Test şube", "test-" + Guid.NewGuid().ToString("N"), "", "Europe/Istanbul", true, 25, hours ?? [])))).GetProperty("id").GetGuid();

    [Fact]
    public async Task Owner_role_and_tenant_scope_cover_every_management_area()
    {
        using var anonymous = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/catalog")).StatusCode);
        using var barista = Client(); await Login(barista, "barista-a@demo.local");
        foreach (var path in new[] { "catalog", "branches", "staff", "payments/iyzico", "reports/summary?from=2026-01-01&to=2026-12-31" }) Assert.Equal(HttpStatusCode.Forbidden, (await barista.GetAsync("/api/admin/" + path)).StatusCode);
        using var a = Client(); using var b = Client(); await Login(a); await Login(b, "owner-b@demo.local");
        var accountA = await Read(await a.GetAsync("/api/admin/payments/iyzico")); var accountB = await Read(await b.GetAsync("/api/admin/payments/iyzico"));
        Assert.NotEqual(accountA.GetProperty("tenantId").GetGuid(), accountB.GetProperty("tenantId").GetGuid()); Assert.False(accountA.GetProperty("credentialsConfigured").GetBoolean()); Assert.False(accountA.GetProperty("checkoutConnected").GetBoolean()); Assert.False(accountA.TryGetProperty("apiKey", out _)); Assert.False(accountA.TryGetProperty("secretKey", out _));
        var foreignCategory = await Category(b);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(a, HttpMethod.Post, "/api/admin/products", Product(foreignCategory))).StatusCode);
        var foreignBranch = (await Read(await b.GetAsync("/api/admin/branches")))[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/admin/reports/summary?from=2026-01-01&to=2026-12-31&branchId={foreignBranch}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(a, HttpMethod.Put, $"/api/admin/branches/{foreignBranch}", new BranchInput("X", "x", "", "UTC", true, 30, [], 1))).StatusCode);
        var foreignStaff = (await Read(await b.GetAsync("/api/admin/staff")))[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await Send(a, HttpMethod.Put, $"/api/admin/staff/{foreignStaff}", new StaffUpdate("Barista", false, [foreignBranch], 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(a, HttpMethod.Post, "/api/admin/staff", new StaffInput("foreign@demo.local", "Barista", [foreignBranch]))).StatusCode);
    }
    [Fact]
    public async Task Catalog_edits_require_csrf_versions_and_owned_option_ids()
    {
        using var c = Client(); await Login(c);
        var category = await Category(c); var input = Product(category, true); var id = await CreateProduct(c, input);
        var path = $"/api/admin/products/{id}";
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(path, input with { ExpectedVersion = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(c, HttpMethod.Put, path, input with { OptionGroups = [new(Guid.NewGuid(), "Foreign", 0, 1, 0, [new(Guid.Empty, "X", 0, true, 0)])], ExpectedVersion = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(c, HttpMethod.Put, path, input with { BasePrice = -1, ExpectedVersion = 1 })).StatusCode);
        (await Send(c, HttpMethod.Put, path, input with { Name = "Düzenlenen", Active = false, OptionGroups = [], ExpectedVersion = 1 })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, HttpMethod.Put, path, input with { ExpectedVersion = 1 })).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.OptionGroups.IgnoreQueryFilters().Where(x => x.ProductId == id).ToListAsync());
        Assert.True(await db.AuditLogs.IgnoreQueryFilters().AnyAsync(x => x.EntityId == id && x.Action == "product.update"));
        await DemoSeed.Run(db, ApiFixture.Password);
        Assert.Empty(await db.OptionGroups.IgnoreQueryFilters().Where(x => x.ProductId == id).ToListAsync());
    }
    [Fact]
    public async Task Branch_override_reprices_quote_and_checkout_keeps_original_snapshot()
    {
        using var c = Client(); await Login(c); var input = Product(await Category(c), true); var id = await CreateProduct(c, input);
        var branchId = await CreateBranch(c);
        var branches = await Read(await c.GetAsync("/api/admin/branches")); var slug = branches.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == branchId).GetProperty("slug").GetString();
        var catalog = await Read(await c.GetAsync("/api/admin/catalog")); var product = catalog.GetProperty("products").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);
        var option = product.GetProperty("optionGroups")[0].GetProperty("values")[0].GetProperty("id").GetGuid();
        var change = new BranchProductInput(true, 120, 1, [new(option, true, 30)]);
        (await Send(c, HttpMethod.Put, $"/api/admin/branches/{branchId}/products/{id}", change)).EnsureSuccessStatusCode();
        var cart = new { branchSlug = slug, items = new[] { new { productId = id, quantity = 1, optionValueIds = new[] { option } } }, expectedTotal = 150 };
        var quote = await Read(await Send(c, HttpMethod.Post, "/api/checkouts/quote", cart)); Assert.Equal(150, quote.GetProperty("total").GetDecimal());
        var csrf = await c.GetFromJsonAsync<JsonElement>("/api/session/csrf"); var checkout = new HttpRequestMessage(HttpMethod.Post, "/api/checkouts") { Content = JsonContent.Create(cart) }; checkout.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString()); checkout.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var paymentUrl = (await Read(await c.SendAsync(checkout))).GetProperty("paymentUrl").GetString()!;
        (await Send(c, HttpMethod.Put, $"/api/admin/products/{id}", input with { Active = false, OptionGroups = [], ExpectedVersion = 1 })).EnsureSuccessStatusCode();
        var payment = await Read(await c.GetAsync("/api/sandbox/sessions/" + paymentUrl.Split('/').Last())); Assert.Equal(150, payment.GetProperty("amount").GetDecimal()); Assert.Equal("Süt", payment.GetProperty("items")[0].GetProperty("options")[0].GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, HttpMethod.Post, "/api/checkouts/quote", cart)).StatusCode);
    }
    [Fact]
    public async Task Schedule_blocks_checkout_and_branch_slug_is_immutable()
    {
        using var c = Client(); await Login(c); var closedDay = ((int)TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DayOfWeek + 1) % 7;
        var id = await CreateBranch(c, [new(closedDay, "09:00", "18:00")]); var branch = (await Read(await c.GetAsync("/api/admin/branches"))).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id); Assert.False(branch.GetProperty("isOpenNow").GetBoolean());
        var slug = branch.GetProperty("slug").GetString(); var menu = await Read(await c.GetAsync($"/api/branches/{slug}/menu")); Assert.False(menu.GetProperty("branch").GetProperty("isAcceptingOrders").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(c, HttpMethod.Put, $"/api/admin/branches/{id}", new BranchInput("X", "changed-slug", "", "UTC", true, 20, [], 1))).StatusCode);
        var product = menu.GetProperty("products")[0];
        var options = product.GetProperty("optionGroups").EnumerateArray().SelectMany(g => g.GetProperty("values").EnumerateArray().Take(g.GetProperty("minSelect").GetInt32())).Select(v => v.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, HttpMethod.Post, "/api/checkouts/quote", new { branchSlug = slug, items = new[] { new { productId = product.GetProperty("id").GetGuid(), quantity = 1, optionValueIds = options } } })).StatusCode);
    }
    [Fact]
    public async Task Invitation_rotation_single_use_activation_and_revocation_work()
    {
        using var owner = Client(); await Login(owner); var branch = (await Read(await owner.GetAsync("/api/admin/branches")))[0].GetProperty("id").GetGuid();
        var email = Guid.NewGuid() + "@demo.local"; var invited = await Read(await Send(owner, HttpMethod.Post, "/api/admin/staff", new StaffInput(email, "Barista", [branch]))); var id = invited.GetProperty("id").GetGuid(); var oldToken = invited.GetProperty("invitationUrl").GetString()!.Split('/').Last();
        var renewed = await Read(await Send(owner, HttpMethod.Post, $"/api/admin/staff/{id}/invitation", new { })); var token = renewed.GetProperty("invitationUrl").GetString()!.Split('/').Last(); using var guest = Client(); Assert.Equal(HttpStatusCode.Gone, (await guest.GetAsync($"/api/invitations/{oldToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(guest, HttpMethod.Post, $"/api/invitations/{token}/redeem", new { password = "short" })).StatusCode);
        (await Send(guest, HttpMethod.Post, $"/api/invitations/{token}/redeem", new { password = ApiFixture.Password })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Gone, (await Send(guest, HttpMethod.Post, $"/api/invitations/{token}/redeem", new { password = ApiFixture.Password })).StatusCode);
        await Login(guest, email); Assert.Single((await Read(await guest.GetAsync("/api/staff/branches"))).EnumerateArray());
        var people = await Read(await owner.GetAsync("/api/admin/staff")); Assert.DoesNotContain("passwordHash", people.GetRawText()); var user = people.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);
        (await Send(owner, HttpMethod.Put, $"/api/admin/staff/{id}", new StaffUpdate("Barista", false, [branch], user.GetProperty("version").GetInt32()))).EnsureSuccessStatusCode(); Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/session/me")).StatusCode);
        var self = people.EnumerateArray().Single(x => x.GetProperty("email").GetString() == "owner-a@demo.local"); Assert.Equal(HttpStatusCode.BadRequest, (await Send(owner, HttpMethod.Put, $"/api/admin/staff/{self.GetProperty("id").GetGuid()}", new StaffUpdate("Barista", false, [branch], self.GetProperty("version").GetInt32()))).StatusCode);
    }
    [Fact]
    public async Task Report_counts_only_paid_cohort_and_measures_actual_preparation()
    {
        using var c = Client(); await Login(c); var branchId = await CreateBranch(c); var now = DateTimeOffset.UtcNow;
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(x => x.Id == branchId);
        Order Make(string payment, string status, decimal amount) => new() { Id = Guid.NewGuid(), TenantId = branch.TenantId, BranchId = branchId, TrackingTokenHash = Guid.NewGuid().ToString(), PaymentStatus = payment, Status = status, Total = amount, CreatedAt = now, PaidAt = payment == "Paid" ? now : null, ExpiresAt = now.AddHours(1) };
        var paid = Make("Paid", "Completed", 150); db.Orders.AddRange(paid, Make("Pending", "PaymentPending", 500), Make("Failed", "PaymentFailed", 100)); db.OrderStatusHistory.AddRange(new() { TenantId = branch.TenantId, OrderId = paid.Id, ToStatus = "Preparing", OccurredAt = now.AddMinutes(-6) }, new() { TenantId = branch.TenantId, OrderId = paid.Id, ToStatus = "Ready", OccurredAt = now.AddMinutes(-2) }); await db.SaveChangesAsync();
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime).ToString("yyyy-MM-dd"); var report = await Read(await c.GetAsync($"/api/admin/reports/summary?from={date}&to={date}&branchId={branchId}")); Assert.Equal(150, report.GetProperty("revenue").GetDecimal()); Assert.Equal(1, report.GetProperty("paidOrders").GetInt32()); Assert.Equal(1, report.GetProperty("completedOrders").GetInt32()); Assert.Equal(1, report.GetProperty("failedPayments").GetInt32()); Assert.Equal(4, report.GetProperty("averagePreparationMinutes").GetDouble()); Assert.Equal(1, report.GetProperty("preparationSamples").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/admin/reports/summary?from=2025-01-01&to=2026-12-31")).StatusCode);
        branch.TimeZone = "America/Sao_Paulo"; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/admin/reports/summary?from=2018-11-04&to=2018-11-04&branchId={branchId}")).StatusCode);
    }
    [Fact]
    public void Hours_use_branch_timezone_and_reject_invalid_windows()
    {
        var branch = new Branch { TimeZone = "Europe/Istanbul", HoursJson = JsonSerializer.Serialize(new[] { new OpeningWindow(1, "09:00", "18:00") }) };
        Assert.True(BranchHours.IsOpen(branch, DateTimeOffset.Parse("2026-10-12T06:00:00Z"))); Assert.False(BranchHours.IsOpen(branch, DateTimeOffset.Parse("2026-10-12T15:00:00Z"))); Assert.False(BranchHours.Valid([new(1, "22:00", "08:00")])); Assert.False(BranchHours.Valid([new(1, "09:00", "18:00"), new(1, "10:00", "19:00")])); Assert.False(BranchHours.Valid([null!]));
    }
}
