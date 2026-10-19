using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Catalog;
using Kahve.Api.Modules.Orders;
using Kahve.Api.Modules.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kahve.Api.Tests;

public sealed class PaymentTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private HttpClient Client() => fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
    private async Task<CheckoutRequest> Cart(HttpClient c)
    {
        var menu = (await c.GetFromJsonAsync<MenuDto>("/api/branches/demo-moda/menu"))!;
        var p = menu.Products[0];
        return new("demo-moda", [new(p.Id, 1, p.OptionGroups.SelectMany(g => g.Values.Where(v => v.Available).Take(g.MinSelect)).Select(v => v.Id).ToList())], p.Price);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient c, string path, object body, Guid? key = null)
    {
        var csrf = await c.GetFromJsonAsync<JsonElement>("/api/session/csrf");
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        if (key is not null) request.Headers.Add("Idempotency-Key", key.ToString());
        return await c.SendAsync(request);
    }
    private async Task<CheckoutReply> Create(HttpClient c)
    {
        var response = await Post(c, "/api/checkouts", await Cart(c), Guid.NewGuid());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CheckoutReply>())!;
    }
    private async Task<Payment> Payment(CheckoutReply reply)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = CapabilityTokens.Hash(reply.PaymentUrl.Split('/').Last());
        return await db.Payments.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.SessionTokenHash == hash);
    }
    private async Task<HttpResponseMessage> Webhook(HttpClient c, Payment payment, string outcome = "paid", string? eventId = null, decimal? amount = null, string currency = "TRY", long? seconds = null, bool tamper = false)
    {
        var provider = fixture.Factory.Services.GetRequiredService<SandboxPaymentProvider>();
        var body = JsonSerializer.Serialize(new SandboxWebhook(eventId ?? Guid.NewGuid().ToString("N"), payment.ProviderReference, outcome, amount ?? payment.Amount, currency), CheckoutEndpoints.Json);
        var timestamp = (seconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/sandbox/webhook") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Sandbox-Timestamp", timestamp);
        request.Headers.Add("X-Sandbox-Signature", tamper ? new string('0', 64) : provider.Sign(timestamp, body));
        return await c.SendAsync(request);
    }
    private async Task Drain()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var pump = scope.ServiceProvider.GetRequiredService<OutboxPump>();
        while (await pump.ProcessOne(default)) { }
    }
    [Fact]
    public async Task Concurrent_checkout_retries_create_one_order_and_same_session()
    {
        using var c = Client();
        var cart = await Cart(c);
        var key = Guid.NewGuid();
        await c.GetAsync("/api/session/csrf");
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Post(c, "/api/checkouts", cart, key)));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var replies = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<CheckoutReply>()));
        Assert.True(replies.All(x => x!.PaymentUrl == replies[0]!.PaymentUrl));
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Payments.IgnoreQueryFilters().CountAsync(x => x.IdempotencyKey == key));
    }
    [Fact]
    public async Task Changed_payload_with_same_key_is_rejected()
    {
        using var c = Client();
        var cart = await Cart(c);
        var key = Guid.NewGuid();
        (await Post(c, "/api/checkouts", cart, key)).EnsureSuccessStatusCode();
        var changed = cart with { Items = [cart.Items![0]! with { Quantity = 2 }], ExpectedTotal = cart.ExpectedTotal * 2 };
        Assert.Equal(HttpStatusCode.Conflict, (await Post(c, "/api/checkouts", changed, key)).StatusCode);
    }
    [Fact]
    public async Task Price_change_and_missing_key_cannot_open_a_payment()
    {
        using var c = Client();
        var cart = await Cart(c);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(c, "/api/checkouts", cart)).StatusCode);
        var key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await Post(c, "/api/checkouts", cart with { ExpectedTotal = 0 }, key)).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments.IgnoreQueryFilters().AnyAsync(x => x.IdempotencyKey == key));
    }
    [Theory]
    [InlineData("signature")]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("amount")]
    [InlineData("currency")]
    public async Task Invalid_webhook_cannot_mark_payment_paid(string kind)
    {
        using var c = Client();
        var payment = await Payment(await Create(c));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var response = await Webhook(c, payment, amount: kind == "amount" ? payment.Amount + 1 : null, currency: kind == "currency" ? "USD" : "TRY", seconds: kind == "stale" ? now - 301 : kind == "future" ? now + 301 : null, tamper: kind == "signature");
        Assert.Equal(kind is "amount" or "currency" ? HttpStatusCode.BadRequest : HttpStatusCode.Unauthorized, response.StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Pending", (await db.Payments.IgnoreQueryFilters().SingleAsync(x => x.Id == payment.Id)).Status);
        Assert.False(await db.PaymentOutbox.IgnoreQueryFilters().AnyAsync(x => x.OrderId == payment.OrderId));
    }
    [Fact]
    public async Task Concurrent_duplicate_webhooks_emit_one_paid_transition_and_one_outbox()
    {
        using var c = Client();
        var payment = await Payment(await Create(c));
        var eventId = Guid.NewGuid().ToString("N");
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Webhook(c, payment, eventId: eventId)));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        (await Webhook(c, payment)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Webhook(c, payment, outcome: "failed", eventId: eventId)).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.PaymentOutbox.IgnoreQueryFilters().CountAsync(x => x.OrderId == payment.OrderId));
        Assert.Equal(1, await db.OrderStatusHistory.IgnoreQueryFilters().CountAsync(x => x.OrderId == payment.OrderId && x.ToStatus == "Paid"));
        await Drain();
        Assert.Equal("Received", (await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == payment.OrderId)).Status);
    }
    [Fact]
    public async Task Failed_payment_never_enters_outbox_and_cannot_be_flipped_to_paid()
    {
        using var c = Client();
        var reply = await Create(c);
        var payment = await Payment(reply);
        var token = reply.PaymentUrl.Split('/').Last();
        var result = await Post(c, $"/api/sandbox/sessions/{token}/decision", new { outcome = "failed" });
        result.EnsureSuccessStatusCode();
        Assert.Equal("Failed", (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("paymentStatus").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Webhook(c, payment)).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.PaymentOutbox.IgnoreQueryFilters().AnyAsync(x => x.OrderId == payment.OrderId));
        Assert.Equal("PaymentFailed", (await db.Orders.IgnoreQueryFilters().SingleAsync(x => x.Id == payment.OrderId)).Status);
    }
    [Fact]
    public async Task Catalog_changes_after_checkout_do_not_change_payment_or_order_snapshot()
    {
        using var c = Client();
        var cart = await Cart(c);
        var reply = (await (await Post(c, "/api/checkouts", cart, Guid.NewGuid())).Content.ReadFromJsonAsync<CheckoutReply>())!;
        var payment = await Payment(reply);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == cart.Items![0]!.ProductId);
        var original = product.Name;
        var price = product.BasePrice;
        product.Name = "Changed product"; product.BasePrice += 20; product.Active = false;
        await db.SaveChangesAsync();
        try
        {
            (await Webhook(c, payment)).EnsureSuccessStatusCode();
            var order = await db.Orders.IgnoreQueryFilters().SingleAsync(x => x.Id == payment.OrderId);
            Assert.Equal(cart.ExpectedTotal, order.Total);
            Assert.Equal(original, JsonSerializer.Deserialize<List<QuotedItem>>(order.ItemsJson, CheckoutEndpoints.Json)![0].Name);
        }
        finally { product.Name = original; product.BasePrice = price; product.Active = true; await db.SaveChangesAsync(); }
        await Drain();
    }
    [Fact]
    public async Task Tracking_requires_the_long_order_capability_and_tokens_are_not_plaintext_in_db()
    {
        using var c = Client();
        var payment = await Payment(await Create(c));
        (await Webhook(c, payment)).EnsureSuccessStatusCode();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.IgnoreQueryFilters().SingleAsync(x => x.Id == payment.OrderId);
        var token = scope.ServiceProvider.GetRequiredService<CapabilityTokens>().Unprotect(order.ProtectedTrackingToken);
        Assert.Equal(43, token.Length);
        Assert.True(order.TrackingTokenHash != token && order.ProtectedTrackingToken != token);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/orders/track/{token}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/orders/track/{order.PublicNumber}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/orders/track/{CapabilityTokens.New()}")).StatusCode);
        await Drain();
    }
    [Fact]
    public async Task Expired_session_is_terminal_and_never_enters_operations()
    {
        using var c = Client();
        var payment = await Payment(await Create(c));
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.IgnoreQueryFilters().SingleAsync(x => x.Id == payment.OrderId);
        order.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Gone, (await Webhook(c, payment)).StatusCode);
        db.ChangeTracker.Clear();
        Assert.Equal("Expired", (await db.Orders.IgnoreQueryFilters().SingleAsync(x => x.Id == payment.OrderId)).Status);
        Assert.False(await db.PaymentOutbox.IgnoreQueryFilters().AnyAsync(x => x.OrderId == payment.OrderId));
    }
    private sealed class FailingReceiver : IOrderReceiver { public void Receive(AppDbContext db, Order order) => throw new InvalidOperationException("Test dispatch failure"); }
    [Fact]
    public async Task Outbox_failure_preserves_paid_order_and_durable_retry_recovers_once()
    {
        await Drain();
        using var c = Client();
        var payment = await Payment(await Create(c));
        (await Webhook(c, payment)).EnsureSuccessStatusCode();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await new OutboxPump(db, new FailingReceiver()).ProcessOne(default));
        db.ChangeTracker.Clear();
        var entry = await db.PaymentOutbox.IgnoreQueryFilters().SingleAsync(x => x.OrderId == payment.OrderId);
        Assert.Null(entry.ProcessedAt); Assert.Equal(1, entry.Attempts); Assert.Equal("dispatch_failed", entry.LastError);
        Assert.Equal("Paid", (await db.Orders.IgnoreQueryFilters().SingleAsync(x => x.Id == payment.OrderId)).Status);
        entry.NextAttemptAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
        await Drain();
        db.ChangeTracker.Clear();
        Assert.NotNull((await db.PaymentOutbox.IgnoreQueryFilters().SingleAsync(x => x.Id == entry.Id)).ProcessedAt);
        Assert.Equal(1, await db.OrderStatusHistory.IgnoreQueryFilters().CountAsync(x => x.OrderId == payment.OrderId && x.ToStatus == "Received"));
    }
    [Fact]
    public async Task Staff_queue_excludes_unpaid_orders_and_is_tenant_scoped()
    {
        using var c = Client();
        var payment = await Payment(await Create(c));
        var menu = (await c.GetFromJsonAsync<MenuDto>("/api/branches/demo-moda/menu"))!;
        (await Post(c, "/api/session/login", new { email = "owner-a@demo.local", password = ApiFixture.Password })).EnsureSuccessStatusCode();
        var orders = await c.GetFromJsonAsync<JsonElement>($"/api/staff/branches/{menu.Branch.Id}/orders");
        Assert.DoesNotContain(orders.EnumerateArray(), x => x.GetProperty("id").GetGuid() == payment.OrderId);
        var foreign = (await c.GetFromJsonAsync<MenuDto>("/api/branches/demo-besiktas/menu"))!;
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/staff/branches/{foreign.Branch.Id}/orders")).StatusCode);
    }
    [Fact]
    public void Terminal_order_status_cannot_return_to_preparing()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Throws<InvalidOperationException>(() => OrderTransitions.Change(db, new Order { Status = "Completed" }, "Preparing", "staff", "test"));
    }
    [Fact]
    public async Task Disabled_sandbox_returns_404_without_resolving_payment_secrets()
    {
        using var disabled = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Sandbox:Enabled", "false");
            b.UseSetting("Payments:WebhookSecret", "");
            b.UseSetting("Tracking:EncryptionKey", "");
        });
        using var c = disabled.CreateClient();
        var menu = await c.GetFromJsonAsync<MenuDto>("/api/branches/demo-moda/menu");
        Assert.False(menu!.CheckoutEnabled);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(c, "/api/checkouts", await Cart(c), Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/sandbox/sessions/{CapabilityTokens.New()}")).StatusCode);
    }
    [Fact]
    public async Task Explicit_none_disables_checkout_and_simulator_without_resolving_secrets()
    {
        using var disabled = fixture.Factory.WithWebHostBuilder(b => { b.UseSetting("Payments:Provider", "none"); b.UseSetting("Payments:WebhookSecret", ""); b.UseSetting("Tracking:EncryptionKey", ""); });
        using var c = disabled.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await Post(c, "/api/checkouts", await Cart(c), Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/sandbox/sessions/{CapabilityTokens.New()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(c, $"/api/sandbox/sessions/{CapabilityTokens.New()}/decision", new { outcome = "paid" })).StatusCode);
    }
    [Fact]
    public async Task Other_provider_payment_is_inaccessible_to_sandbox_and_missing_adapter_retry_creates_nothing()
    {
        using var c = Client(); var cart = await Cart(c); var key = Guid.NewGuid();
        var response = await Post(c, "/api/checkouts", cart, key); response.EnsureSuccessStatusCode(); var reply = (await response.Content.ReadFromJsonAsync<CheckoutReply>())!; var payment = await Payment(reply);
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Payments.IgnoreQueryFilters().SingleAsync(x => x.Id == payment.Id); stored.Provider = "not-installed"; await db.SaveChangesAsync();
        var token = reply.PaymentUrl.Split('/').Last();
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/sandbox/sessions/{token}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(c, $"/api/sandbox/sessions/{token}/decision", new { outcome = "paid" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Webhook(c, payment)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Post(c, "/api/checkouts", cart, key)).StatusCode);
        Assert.Equal(1, await db.Payments.IgnoreQueryFilters().CountAsync(x => x.IdempotencyKey == key));
        Assert.False(await db.PaymentOutbox.IgnoreQueryFilters().AnyAsync(x => x.OrderId == payment.OrderId));
    }
    private sealed class TestContinuationProvider : IPaymentProvider
    {
        public string Name => "test-continuation";
        public string CreateReference() => Guid.NewGuid().ToString("N");
        public string CheckoutPath(string token) => $"/test/pay/{token}";
        public void ValidateConfiguration() { }
    }
    [Fact]
    public async Task Idempotent_retry_routes_using_stored_provider_instead_of_current_selection()
    {
        using var alternate = fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IPaymentProvider, TestContinuationProvider>()));
        using var c = alternate.CreateClient(); var cart = await Cart(c); var key = Guid.NewGuid();
        var created = await Post(c, "/api/checkouts", cart, key); created.EnsureSuccessStatusCode(); var original = (await created.Content.ReadFromJsonAsync<CheckoutReply>())!;
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var payment = await db.Payments.IgnoreQueryFilters().SingleAsync(x => x.IdempotencyKey == key); payment.Provider = "test-continuation"; await db.SaveChangesAsync();
        var repeated = await Post(c, "/api/checkouts", cart, key); repeated.EnsureSuccessStatusCode(); var reply = (await repeated.Content.ReadFromJsonAsync<CheckoutReply>())!;
        Assert.Equal($"/test/pay/{original.PaymentUrl.Split('/').Last()}", reply.PaymentUrl); Assert.Equal(original.Amount, reply.Amount); Assert.Equal(original.ExpiresAt, reply.ExpiresAt); Assert.Equal(1, await db.Payments.IgnoreQueryFilters().CountAsync(x => x.IdempotencyKey == key));
    }
}
