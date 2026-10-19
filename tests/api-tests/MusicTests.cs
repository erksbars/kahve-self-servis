using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Catalog;
using Kahve.Api.Modules.Music;
using Kahve.Api.Modules.Orders;
using Kahve.Api.Modules.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kahve.Api.Tests;
public sealed class MusicTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private HttpClient Client() => fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
    private static async Task<JsonElement> Read(HttpResponseMessage response) { response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
    private static async Task<HttpResponseMessage> Send(HttpClient c, HttpMethod method, string path, object body, Guid? key = null)
    {
        var csrf = await c.GetFromJsonAsync<JsonElement>("/api/session/csrf");
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        if (key is not null) request.Headers.Add("Idempotency-Key", key.ToString());
        return await c.SendAsync(request);
    }
    private static async Task Login(HttpClient c, string email = "owner-a@demo.local") => (await Send(c, HttpMethod.Post, "/api/session/login", new { email, password = ApiFixture.Password })).EnsureSuccessStatusCode();
    private async Task<CheckoutRequest> Cart(HttpClient c, string? text)
    {
        var menu = (await c.GetFromJsonAsync<MenuDto>("/api/branches/demo-moda/menu"))!;
        var p = menu.Products.First(p => p.OptionGroups.All(g => g.MinSelect == 0));
        return new("demo-moda", [new(p.Id, 1, [])], p.Price, text);
    }
    private async Task Pay(HttpClient c, Payment payment)
    {
        var provider = fixture.Factory.Services.GetRequiredService<SandboxPaymentProvider>();
        var body = JsonSerializer.Serialize(new SandboxWebhook(Guid.NewGuid().ToString("N"), payment.ProviderReference, "paid", payment.Amount, payment.Currency), CheckoutEndpoints.Json);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/sandbox/webhook") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Sandbox-Timestamp", timestamp); request.Headers.Add("X-Sandbox-Signature", provider.Sign(timestamp, body));
        (await c.SendAsync(request)).EnsureSuccessStatusCode();
        using var scope = fixture.Factory.Services.CreateScope();
        var pump = scope.ServiceProvider.GetRequiredService<OutboxPump>(); while (await pump.ProcessOne(default)) { }
    }
    [Fact]
    public async Task Checkout_attaches_one_text_request_and_only_paid_orders_reach_the_panel()
    {
        using var customer = Client(); using var owner = Client(); await Login(owner);
        var cart = await Cart(customer, "  Scorpions — Still Loving You  "); var key = Guid.NewGuid();
        await customer.GetAsync("/api/session/csrf");
        var replies = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Send(customer, HttpMethod.Post, "/api/checkouts", cart, key)));
        Assert.All(replies, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payment = await db.Payments.IgnoreQueryFilters().SingleAsync(p => p.IdempotencyKey == key);
        var row = await db.SongRequests.IgnoreQueryFilters().SingleAsync(r => r.OrderId == payment.OrderId);
        Assert.Equal("Scorpions — Still Loving You", row.RequestedText); Assert.Null(row.SongId); Assert.Null(row.GuestKeyHash);
        var path = $"/api/staff/branches/{payment.BranchId}/song-requests";
        var order = await db.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == payment.OrderId);
        var tracking = "/api/orders/track/" + fixture.Factory.Services.GetRequiredService<CapabilityTokens>().Unprotect(order.ProtectedTrackingToken);
        Assert.Equal(JsonValueKind.Null, (await Read(await customer.GetAsync(tracking))).GetProperty("songRequest").ValueKind);
        Assert.DoesNotContain((await Read(await owner.GetAsync(path))).GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == row.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(customer, HttpMethod.Post, "/api/checkouts", cart with { RequestedSong = "Different" }, key)).StatusCode);
        await Pay(customer, payment);
        Assert.Equal(row.RequestedText, (await Read(await customer.GetAsync(tracking))).GetProperty("songRequest").GetProperty("text").GetString());
        var inbox = await Read(await owner.GetAsync(path)); var entry = inbox.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == row.Id);
        Assert.Equal(row.RequestedText, entry.GetProperty("text").GetString()); Assert.True(entry.GetProperty("publicNumber").GetInt64() > 0);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(path + $"/{row.Id}", new RequestSongDecision("Accepted", 1))).StatusCode);
        (await Send(owner, HttpMethod.Put, path + $"/{row.Id}", new RequestSongDecision("Accepted", 1))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Send(owner, HttpMethod.Put, path + $"/{row.Id}", new RequestSongDecision("Rejected", 1))).StatusCode);
        await db.Entry(row).ReloadAsync(); Assert.Equal("Accepted", row.Status);
        Assert.Equal("Accepted", (await Read(await customer.GetAsync(tracking))).GetProperty("songRequest").GetProperty("status").GetString());
    }
    [Fact]
    public async Task Song_is_optional_and_long_text_cannot_create_checkout_or_request()
    {
        using var c = Client(); var cart = await Cart(c, null);
        foreach (var text in new string?[] { null, "   " })
        {
            var key = Guid.NewGuid(); (await Send(c, HttpMethod.Post, "/api/checkouts", cart with { RequestedSong = text }, key)).EnsureSuccessStatusCode();
            using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Payments.IgnoreQueryFilters().Where(p => p.IdempotencyKey == key).Select(p => p.OrderId).SingleAsync();
            Assert.False(await db.SongRequests.IgnoreQueryFilters().AnyAsync(r => r.OrderId == order));
        }
        var invalidKey = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(c, HttpMethod.Post, "/api/checkouts", cart with { RequestedSong = new string('a', 241) }, invalidKey)).StatusCode);
        using var check = fixture.Factory.Services.CreateScope(); Assert.False(await check.ServiceProvider.GetRequiredService<AppDbContext>().Payments.IgnoreQueryFilters().AnyAsync(p => p.IdempotencyKey == invalidKey));
    }
    [Fact]
    public async Task Panel_and_moderation_require_branch_access_and_never_cross_tenants()
    {
        using var owner = Client(); using var barista = Client(); using var foreign = Client(); using var anonymous = Client();
        await Login(owner); await Login(barista, "barista-a@demo.local"); await Login(foreign, "owner-b@demo.local");
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.Slug == "demo-moda");
        var other = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.Slug == "demo-besiktas");
        var order = new Order { Id = Guid.NewGuid(), TenantId = branch.TenantId, BranchId = branch.Id, TrackingTokenHash = CapabilityTokens.Hash(CapabilityTokens.New()), ProtectedTrackingToken = "test", PaymentStatus = "Paid", Status = "Received", CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15) };
        var row = new SongRequest { Id = Guid.NewGuid(), TenantId = branch.TenantId, BranchId = branch.Id, OrderId = order.Id, RequestedText = "Bir istek", CreatedAt = DateTimeOffset.UtcNow };
        db.Orders.Add(order); db.SongRequests.Add(row); await db.SaveChangesAsync();
        var path = $"/api/staff/branches/{branch.Id}/song-requests";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(foreign, HttpMethod.Put, $"/api/staff/branches/{other.Id}/song-requests/{row.Id}", new RequestSongDecision("Accepted", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await barista.GetAsync($"/api/staff/branches/{other.Id}/song-requests")).StatusCode);
        Assert.Contains((await Read(await barista.GetAsync(path))).GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == row.Id);
        (await Send(barista, HttpMethod.Put, path + $"/{row.Id}", new RequestSongDecision("Rejected", 1))).EnsureSuccessStatusCode();
        order.Status = "Cancelled"; await db.SaveChangesAsync();
        Assert.DoesNotContain((await Read(await owner.GetAsync(path))).GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == row.Id);
    }
    [Fact]
    public async Task Standalone_catalog_and_post_checkout_suggestion_routes_are_retired()
    {
        using var c = Client();
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/branches/demo-moda/music")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/orders/track/anything/music")).StatusCode);
    }
    [Fact]
    public async Task Server_filters_before_paging_and_counts_all_old_pending_requests()
    {
        using var owner = Client(); await Login(owner);
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = await db.Branches.IgnoreQueryFilters().Where(b => b.Slug == "demo-moda").Select(b => b.TenantId).SingleAsync();
        var branch = new Kahve.Api.Modules.Tenancy.Branch { Id = Guid.NewGuid(), TenantId = tenant, Name = "Paging test", Slug = "paging-" + Guid.NewGuid().ToString("N") };
        db.Branches.Add(branch);
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < 145; index++)
        {
            var order = new Order { Id = Guid.NewGuid(), TenantId = tenant, BranchId = branch.Id, TrackingTokenHash = CapabilityTokens.Hash(CapabilityTokens.New()), ProtectedTrackingToken = "test", PaymentStatus = "Paid", Status = "Received", CreatedAt = now, ExpiresAt = now.AddMinutes(15) };
            db.Orders.Add(order);
            db.SongRequests.Add(new SongRequest { Id = Guid.NewGuid(), TenantId = tenant, BranchId = branch.Id, OrderId = order.Id, RequestedText = "Test " + index, Status = index < 25 ? "Pending" : "Accepted", CreatedAt = index < 25 ? now.AddDays(-1) : now });
        }
        await db.SaveChangesAsync();
        var path = $"/api/staff/branches/{branch.Id}/song-requests";
        var first = await Read(await owner.GetAsync(path));
        Assert.Equal(25, first.GetProperty("pendingCount").GetInt32()); Assert.Equal(25, first.GetProperty("total").GetInt32());
        var firstIds = first.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToArray(); Assert.Equal(20, firstIds.Length);
        var second = await Read(await owner.GetAsync(path + "?page=2"));
        var secondIds = second.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(5, secondIds.Length); Assert.Empty(firstIds.Intersect(secondIds));
        Assert.All(second.GetProperty("items").EnumerateArray(), r => Assert.Equal("Pending", r.GetProperty("status").GetString()));
        var accepted = await Read(await owner.GetAsync(path + "?status=Accepted"));
        Assert.Equal(120, accepted.GetProperty("total").GetInt32()); Assert.Equal(25, accepted.GetProperty("pendingCount").GetInt32());
        Assert.All(accepted.GetProperty("items").EnumerateArray(), r => Assert.Equal("Accepted", r.GetProperty("status").GetString()));
        var all = await Read(await owner.GetAsync(path + "?status=All")); Assert.Equal(145, all.GetProperty("total").GetInt32());
        var clamped = await Read(await owner.GetAsync(path + "?page=9999")); Assert.Equal(2, clamped.GetProperty("page").GetInt32());
        foreach (var query in new[] { "?status=Other", "?page=0", "?pageSize=51" }) Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync(path + query)).StatusCode);
        foreach (var id in secondIds) (await Send(owner, HttpMethod.Put, path + $"/{id}", new RequestSongDecision("Accepted", 1))).EnsureSuccessStatusCode();
        var after = await Read(await owner.GetAsync(path + "?page=2"));
        Assert.Equal(20, after.GetProperty("pendingCount").GetInt32()); Assert.Equal(1, after.GetProperty("page").GetInt32());
    }
}
