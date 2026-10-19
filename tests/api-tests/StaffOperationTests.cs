using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Catalog;
using Kahve.Api.Modules.Orders;
using Kahve.Api.Modules.Payments;
using Kahve.Api.Modules.Realtime;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kahve.Api.Tests;
public sealed class StaffOperationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private HttpClient Client() => fixture.Factory.CreateClient();
    private static async Task<HttpResponseMessage> Mutate(HttpClient c, HttpMethod method, string path, object body, Guid? key = null)
    {
        var csrf = await c.GetFromJsonAsync<JsonElement>("/api/session/csrf");
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        if (key.HasValue) request.Headers.Add("Idempotency-Key", key.ToString());
        return await c.SendAsync(request);
    }
    private static async Task Login(HttpClient c, string email = "barista-a@demo.local") => (await Mutate(c, HttpMethod.Post, "/api/session/login", new { email, password = ApiFixture.Password })).EnsureSuccessStatusCode();
    private async Task<Order> Create(bool paid = true, string slug = "demo-moda")
    {
        using var c = Client();
        var menu = (await c.GetFromJsonAsync<MenuDto>($"/api/branches/{slug}/menu"))!;
        var p = menu.Products[0];
        var ids = p.OptionGroups.SelectMany(g => g.Values.Where(v => v.Available).Take(g.MinSelect)).Select(v => v.Id).ToList();
        var created = await Mutate(c, HttpMethod.Post, "/api/checkouts", new CheckoutRequest(slug, [new(p.Id, 1, ids)], p.Price), Guid.NewGuid());
        created.EnsureSuccessStatusCode();
        var reply = (await created.Content.ReadFromJsonAsync<CheckoutReply>())!;
        var token = reply.PaymentUrl.Split('/').Last();
        if (paid) (await Mutate(c, HttpMethod.Post, $"/api/sandbox/sessions/{token}/decision", new { outcome = "paid" })).EnsureSuccessStatusCode();
        using var scope = fixture.Factory.Services.CreateScope();
        var pump = scope.ServiceProvider.GetRequiredService<OutboxPump>();
        while (await pump.ProcessOne(default)) { }
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payment = await db.Payments.IgnoreQueryFilters().SingleAsync(x => x.SessionTokenHash == CapabilityTokens.Hash(token));
        return await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == payment.OrderId);
    }
    private static Task<HttpResponseMessage> Patch(HttpClient c, Order o, string next, int? version = null) => Mutate(c, HttpMethod.Patch, $"/api/staff/orders/{o.Id}/status", new StatusRequest(next, version ?? o.Version));
    [Fact]
    public async Task Barista_can_prepare_ready_and_complete_with_history_audit_and_realtime_events()
    {
        var order = await Create();
        using var c = Client(); await Login(c);
        var version = order.Version;
        foreach (var next in new[] { "Preparing", "Ready", "Completed" }) (await Patch(c, order, next, version++)).EnsureSuccessStatusCode();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var current = await db.Orders.IgnoreQueryFilters().SingleAsync(x => x.Id == order.Id);
        Assert.Equal("Completed", current.Status);
        Assert.Equal(3, await db.AuditLogs.IgnoreQueryFilters().CountAsync(x => x.EntityId == order.Id));
        Assert.Equal(3, await db.OrderStatusHistory.IgnoreQueryFilters().CountAsync(x => x.OrderId == order.Id && x.Actor.StartsWith("staff:")));
        Assert.Equal(5, await db.RealtimeEvents.IgnoreQueryFilters().CountAsync(x => x.OrderId == order.Id));
        var token = scope.ServiceProvider.GetRequiredService<CapabilityTokens>().Unprotect(current.ProtectedTrackingToken);
        Assert.Equal("Completed", (await c.GetFromJsonAsync<JsonElement>($"/api/orders/track/{token}")).GetProperty("status").GetString());
        var queue = await c.GetFromJsonAsync<JsonElement>($"/api/staff/branches/{order.BranchId}/orders");
        Assert.DoesNotContain(queue.EnumerateArray(), x => x.GetProperty("id").GetGuid() == order.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Patch(c, order, "Preparing", current.Version)).StatusCode);
    }
    [Fact]
    public async Task Two_concurrent_baristas_cannot_silently_overwrite_the_same_version()
    {
        var order = await Create();
        using var c = Client(); await Login(c);
        var replies = await Task.WhenAll(Patch(c, order, "Preparing"), Patch(c, order, "Preparing"));
        Assert.Single(replies, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(replies, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.OrderStatusHistory.IgnoreQueryFilters().CountAsync(x => x.OrderId == order.Id && x.ToStatus == "Preparing"));
    }
    [Fact]
    public async Task Unpaid_skipped_transition_and_missing_csrf_are_rejected()
    {
        var unpaid = await Create(false);
        var paid = await Create();
        using var c = Client(); await Login(c);
        Assert.Equal(HttpStatusCode.Conflict, (await Patch(c, unpaid, "Preparing")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Patch(c, paid, "Ready")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PatchAsync($"/api/staff/orders/{paid.Id}/status", JsonContent.Create(new StatusRequest("Preparing", paid.Version)))).StatusCode);
    }
    [Fact]
    public async Task Foreign_tenant_and_unassigned_same_tenant_branch_cannot_be_updated()
    {
        var own = await Create(); var second = await Create(slug: "demo-moda-2");
        using var c = Client(); await Login(c);
        Assert.Equal(HttpStatusCode.NotFound, (await Patch(c, second, "Preparing")).StatusCode);
        using var foreign = Client(); await Login(foreign, "owner-b@demo.local");
        Assert.Equal(HttpStatusCode.NotFound, (await Patch(foreign, own, "Preparing")).StatusCode);
    }
    private sealed class TestContext(ClaimsPrincipal user) : HubCallerContext
    {
        public override string ConnectionId => "test-connection";
        public override string? UserIdentifier => user.FindFirstValue(ClaimTypes.NameIdentifier);
        public override ClaimsPrincipal User => user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => default;
        public override void Abort() { }
    }
    private sealed class TestGroups : IGroupManager
    {
        public List<string> Added { get; } = []; public List<string> Removed { get; } = [];
        public Task AddToGroupAsync(string id, string group, CancellationToken ct = default) { Added.Add(group); return Task.CompletedTask; }
        public Task RemoveFromGroupAsync(string id, string group, CancellationToken ct = default) { Removed.Add(group); return Task.CompletedTask; }
    }
    [Fact]
    public async Task Hub_requires_verified_staff_branch_access_and_tracking_capability()
    {
        var order = await Create();
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.StaffUsers.IgnoreQueryFilters().SingleAsync(x => x.Email == "barista-a@demo.local");
        var groups = new TestGroups(); var registry = new StaffSubscriptions();
        var hub = new OrderHub(db, registry) { Context = new TestContext(new()), Groups = groups };
        await Assert.ThrowsAsync<HubException>(() => hub.JoinStaff(order.BranchId));
        await Assert.ThrowsAsync<HubException>(() => hub.JoinTracking(CapabilityTokens.New()));
        var claims = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, user.Id.ToString()), new("tenant_id", user.TenantId.ToString()), new(ClaimTypes.Role, user.Role)], "test"));
        hub.Context = new TestContext(claims);
        var foreign = await db.Branches.IgnoreQueryFilters().SingleAsync(x => x.Slug == "demo-besiktas");
        var second = await db.Branches.IgnoreQueryFilters().SingleAsync(x => x.Slug == "demo-moda-2");
        await Assert.ThrowsAsync<HubException>(() => hub.JoinStaff(foreign.Id));
        await Assert.ThrowsAsync<HubException>(() => hub.JoinStaff(second.Id));
        await hub.JoinStaff(order.BranchId);
        var token = scope.ServiceProvider.GetRequiredService<CapabilityTokens>().Unprotect(order.ProtectedTrackingToken);
        await hub.JoinTracking(token);
        Assert.Contains(OrderHub.StaffGroup(order.BranchId), groups.Added);
        Assert.Contains(OrderHub.TrackingGroup(order.Id), groups.Added);
        Assert.Equal(user.TenantId, registry.Connections["test-connection"].TenantId);
        await hub.OnDisconnectedAsync(null); Assert.Empty(registry.Connections);
    }
    [Fact]
    public async Task Hub_rejects_cross_origin_browser_negotiation()
    {
        using var c = Client();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/orders/negotiate?negotiateVersion=1");
        request.Headers.Add("Origin", "https://another.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await c.SendAsync(request)).StatusCode);
    }
    private sealed class RecordingClients : IHubClients
    {
        public bool FailNext { get; set; }
        public List<(string Target, string Method)> Sent { get; } = [];
        private sealed class Proxy(RecordingClients owner, string target) : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken ct = default)
            {
                if (owner.FailNext) { owner.FailNext = false; throw new InvalidOperationException("Test delivery failure"); }
                owner.Sent.Add((target, method)); return Task.CompletedTask;
            }
        }
        public IClientProxy All => new Proxy(this, "all");
        public IClientProxy AllExcept(IReadOnlyList<string> ids) => All;
        public IClientProxy Client(string id) => new Proxy(this, "client:" + id);
        public IClientProxy Clients(IReadOnlyList<string> ids) => All;
        public IClientProxy Group(string group) => new Proxy(this, "group:" + group);
        public IClientProxy GroupExcept(string group, IReadOnlyList<string> ids) => Group(group);
        public IClientProxy Groups(IReadOnlyList<string> groups) => All;
        public IClientProxy User(string user) => All;
        public IClientProxy Users(IReadOnlyList<string> users) => All;
    }
    private sealed class RecordingHub(RecordingClients clients, TestGroups groups) : IHubContext<OrderHub>
    {
        public IHubClients Clients => clients;
        public IGroupManager Groups => groups;
    }
    [Fact]
    public async Task Revoked_staff_membership_is_removed_before_the_next_group_event()
    {
        var order = await Create();
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clients = new RecordingClients(); var groups = new TestGroups(); var registry = new StaffSubscriptions();
        var pump = new RealtimePump(db, new RecordingHub(clients, groups), registry);
        while (await pump.ProcessOne(default)) { }
        var user = await db.StaffUsers.IgnoreQueryFilters().SingleAsync(x => x.Email == "barista-a@demo.local");
        registry.Connections["revoked"] = new(user.TenantId, user.Id, order.BranchId, user.Role);
        user.Active = false;
        db.RealtimeEvents.Add(new() { Id = Guid.NewGuid(), TenantId = order.TenantId, BranchId = order.BranchId, Version = 1, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        try
        {
            Assert.True(await pump.ProcessOne(default));
            Assert.Empty(registry.Connections);
            Assert.Contains(OrderHub.StaffGroup(order.BranchId), groups.Removed);
            Assert.Contains(("client:revoked", "AccessRevoked"), clients.Sent);
        }
        finally { db.ChangeTracker.Clear(); user = await db.StaffUsers.IgnoreQueryFilters().SingleAsync(x => x.Id == user.Id); user.Active = true; await db.SaveChangesAsync(); }
    }
    [Fact]
    public async Task Failed_realtime_delivery_leaves_a_durable_event_for_retry()
    {
        var order = await Create();
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clients = new RecordingClients(); var pump = new RealtimePump(db, new RecordingHub(clients, new TestGroups()), new());
        while (await pump.ProcessOne(default)) { }
        var id = Guid.NewGuid();
        db.RealtimeEvents.Add(new() { Id = id, TenantId = order.TenantId, BranchId = order.BranchId, OrderId = order.Id, Version = order.Version, CreatedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync();
        clients.FailNext = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => pump.ProcessOne(default));
        db.ChangeTracker.Clear();
        Assert.Null((await db.RealtimeEvents.IgnoreQueryFilters().SingleAsync(x => x.Id == id)).ProcessedAt);
        Assert.True(await pump.ProcessOne(default));
        db.ChangeTracker.Clear();
        Assert.NotNull((await db.RealtimeEvents.IgnoreQueryFilters().SingleAsync(x => x.Id == id)).ProcessedAt);
        Assert.Contains(("group:" + OrderHub.TrackingGroup(order.Id), "OrderChanged"), clients.Sent);
    }
}
