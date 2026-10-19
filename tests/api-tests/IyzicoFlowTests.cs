using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Catalog;
using Kahve.Api.Modules.Orders;
using Kahve.Api.Modules.Payments;
using Kahve.Api.Modules.Payments.Iyzico;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kahve.Api.Tests;
public sealed class IyzicoFlowTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed class Fake : IIyzicoCheckoutClient
    {
        public int Calls; public bool Ambiguous; public bool Rejected; public IyzicoOutcome Outcome = IyzicoOutcome.Confirmed; public bool WrongAmount;
        public IyzicoInitializeRequest? Last;
        public Task<IyzicoSession> Initialize(IyzicoCredentials credentials, IyzicoInitializeRequest input, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls); Last = input;
            if (Ambiguous) throw new IyzicoProtocolException("test_timeout", true);
            if (Rejected) throw new IyzicoProtocolException("provider_rejected_5", false);
            return Task.FromResult(new IyzicoSession("provider-" + input.ConversationId, new Uri("https://sandbox-cpp.iyzipay.com/?token=test")));
        }
        public Task<IyzicoResult> Retrieve(IyzicoCredentials credentials, IyzicoExpectedPayment expected, CancellationToken ct)
        {
            if (expected.Token != "provider-" + expected.ConversationId) throw new IyzicoProtocolException("invalid_token");
            return Task.FromResult(new IyzicoResult("external-" + expected.ConversationId, Outcome, Outcome == IyzicoOutcome.Failed ? 0 : WrongAmount ? 1 : expected.Amount, "TRY", Last!.Items.Select(x => new IyzicoLineResult(x.Id, "transaction-" + x.Id, x.Price, x.Price)).ToList()));
        }
    }
    private async Task<WebApplicationFactory<Program>> Host(Fake fake)
    {
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(x => x.Slug == "demo-moda");
        return fixture.Factory.WithWebHostBuilder(b => { b.UseSetting("Payments:Provider", "iyzico"); b.UseSetting("Iyzico:CallbackOrigin", "https://callback.example.test"); b.UseSetting($"Iyzico:Merchants:{branch.TenantId:N}:ApiKey", "sandbox-test-key"); b.UseSetting($"Iyzico:Merchants:{branch.TenantId:N}:SecretKey", "sandbox-test-secret"); b.ConfigureTestServices(s => { s.RemoveAll<IIyzicoCheckoutClient>(); s.AddSingleton<IIyzicoCheckoutClient>(fake); }); });
    }
    private static async Task<HttpResponseMessage> Post(HttpClient c, string path, object body, Guid? key = null)
    {
        var csrf = await c.GetFromJsonAsync<JsonElement>("/api/session/csrf"); var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) }; request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString()); if (key.HasValue) request.Headers.Add("Idempotency-Key", key.ToString()); return await c.SendAsync(request);
    }
    private static async Task<CheckoutRequest> Cart(HttpClient c, string slug = "demo-moda")
    {
        var menu = (await c.GetFromJsonAsync<MenuDto>($"/api/branches/{slug}/menu"))!; var p = menu.Products[0]; return new(slug, [new(p.Id, 1, p.OptionGroups.SelectMany(g => g.Values.Take(g.MinSelect)).Select(x => x.Id).ToList())], p.Price);
    }
    private async Task<(Payment payment, string token)> Create(HttpClient c)
    {
        var response = await Post(c, "/api/checkouts", await Cart(c), Guid.NewGuid()); response.EnsureSuccessStatusCode(); var reply = (await response.Content.ReadFromJsonAsync<CheckoutReply>())!; var token = reply.PaymentUrl.Split('/').Last();
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var payment = await db.Payments.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.SessionTokenHash == CapabilityTokens.Hash(token)); Assert.Equal("iyzico", payment.Provider); return (payment, token);
    }
    private static IyzicoCustomer Buyer() => new("Test", "Buyer", "test-identity", "buyer@example.test", "+900000000000", "Test billing address", "Istanbul", "Turkey");
    private static string Init(string token) => $"/api/iyzico/sessions/{token}/initialize";
    private static Task<HttpResponseMessage> Callback(HttpClient c, Payment payment, string? token = null) => c.PostAsync($"/api/payments/iyzico/callback/{payment.ProviderReference}", new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token ?? "provider-" + payment.ProviderReference }));
    [Fact]
    public async Task Explicit_provider_rejection_allows_corrected_buyer_without_another_order()
    {
        var fake = new Fake { Rejected = true }; using var host = await Host(fake); using var c = host.CreateClient(new() { AllowAutoRedirect = false }); var (payment, token) = await Create(c);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(c, Init(token), Buyer())).StatusCode);
        var state = await c.GetFromJsonAsync<JsonElement>($"/api/iyzico/sessions/{token}"); Assert.Equal("Rejected", state.GetProperty("initializationState").GetString()); Assert.Equal("Pending", state.GetProperty("paymentStatus").GetString());
        fake.Rejected = false; (await Post(c, Init(token), Buyer() with { Email = "email@email.com" })).EnsureSuccessStatusCode();
        (await Post(c, Init(token), Buyer())).EnsureSuccessStatusCode(); Assert.Equal(2, fake.Calls);
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Equal(payment.OrderId, (await db.Payments.IgnoreQueryFilters().SingleAsync(x => x.Id == payment.Id)).OrderId);
    }
    [Fact]
    public async Task Concurrent_initialize_and_retries_create_one_provider_session_and_encrypt_token()
    {
        var fake = new Fake(); using var host = await Host(fake); using var c = host.CreateClient(new() { AllowAutoRedirect = false }); var (payment, token) = await Create(c); await c.GetAsync("/api/session/csrf");
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Post(c, Init(token), Buyer()))); Assert.All(results, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict)); Assert.Equal(1, fake.Calls);
        (await Post(c, Init(token), Buyer())).EnsureSuccessStatusCode(); Assert.Equal(1, fake.Calls);
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var stored = await db.Payments.IgnoreQueryFilters().SingleAsync(x => x.Id == payment.Id); Assert.Equal("Ready", stored.InitializationState); Assert.DoesNotContain("provider-", stored.ProtectedProviderToken); Assert.DoesNotContain("Test billing", stored.ProviderResultJson);
    }
    [Fact]
    public async Task Callback_verifies_late_payment_atomically_and_duplicate_callback_does_not_duplicate_outbox()
    {
        var fake = new Fake(); using var host = await Host(fake); using var c = host.CreateClient(new() { AllowAutoRedirect = false }); var (payment, token) = await Create(c); (await Post(c, Init(token), Buyer())).EnsureSuccessStatusCode();
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var order = await db.Orders.IgnoreQueryFilters().SingleAsync(x => x.Id == payment.OrderId); order.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Found, (await Callback(c, payment)).StatusCode); Assert.Equal(HttpStatusCode.Found, (await Callback(c, payment)).StatusCode); db.ChangeTracker.Clear(); Assert.Equal("Paid", (await db.Payments.IgnoreQueryFilters().SingleAsync(x => x.Id == payment.Id)).Status); Assert.Equal(1, await db.PaymentOutbox.IgnoreQueryFilters().CountAsync(x => x.OrderId == payment.OrderId)); Assert.Equal(1, await db.OrderStatusHistory.IgnoreQueryFilters().CountAsync(x => x.OrderId == payment.OrderId && x.ToStatus == "Paid"));
    }
    [Fact]
    public async Task Fraud_review_stays_pending_and_later_verify_can_confirm_without_second_charge()
    {
        var fake = new Fake { Outcome = IyzicoOutcome.Review }; using var host = await Host(fake); using var c = host.CreateClient(new() { AllowAutoRedirect = false }); var (payment, token) = await Create(c); (await Post(c, Init(token), Buyer())).EnsureSuccessStatusCode(); Assert.Equal(HttpStatusCode.Found, (await Callback(c, payment)).StatusCode);
        var first = await c.GetFromJsonAsync<JsonElement>($"/api/iyzico/sessions/{token}"); Assert.True(first.GetProperty("review").GetBoolean()); Assert.Equal("Pending", first.GetProperty("paymentStatus").GetString()); fake.Outcome = IyzicoOutcome.Confirmed;
        var verified = await Post(c, $"/api/iyzico/sessions/{token}/verify", new { }); verified.EnsureSuccessStatusCode(); Assert.Equal("Paid", (await verified.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("paymentStatus").GetString()); Assert.Equal(1, fake.Calls);
    }
    [Fact]
    public async Task Ambiguous_initialize_is_never_retried_and_verified_callback_recovers_the_payment()
    {
        var fake = new Fake { Ambiguous = true }; using var host = await Host(fake); using var c = host.CreateClient(new() { AllowAutoRedirect = false }); var (payment, token) = await Create(c);
        Assert.Equal(HttpStatusCode.BadGateway, (await Post(c, Init(token), Buyer())).StatusCode); Assert.Equal(HttpStatusCode.Conflict, (await Post(c, Init(token), Buyer())).StatusCode); Assert.Equal(1, fake.Calls); Assert.Equal(HttpStatusCode.Found, (await Callback(c, payment)).StatusCode);
        var result = await c.GetFromJsonAsync<JsonElement>($"/api/iyzico/sessions/{token}"); Assert.Equal("Paid", result.GetProperty("paymentStatus").GetString());
    }
    [Fact]
    public async Task Forged_callback_wrong_amount_and_missing_csrf_cannot_mark_an_order_paid()
    {
        var fake = new Fake { WrongAmount = true }; using var host = await Host(fake); using var c = host.CreateClient(new() { AllowAutoRedirect = false }); var (payment, token) = await Create(c);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Init(token), Buyer())).StatusCode); Assert.Equal(0, fake.Calls); (await Post(c, Init(token), Buyer())).EnsureSuccessStatusCode(); Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Callback(c, payment, "forged-token")).StatusCode); Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Callback(c, payment)).StatusCode);
        var result = await c.GetFromJsonAsync<JsonElement>($"/api/iyzico/sessions/{token}"); Assert.Equal("Pending", result.GetProperty("paymentStatus").GetString());
    }
    [Fact]
    public async Task A_second_merchant_without_credentials_cannot_use_the_first_account()
    {
        var fake = new Fake(); using var host = await Host(fake); using var c = host.CreateClient(new() { AllowAutoRedirect = false }); var menu = (await c.GetFromJsonAsync<MenuDto>("/api/branches/demo-besiktas/menu"))!; Assert.False(menu.CheckoutEnabled); Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Post(c, "/api/checkouts", await Cart(c, "demo-besiktas"), Guid.NewGuid())).StatusCode); Assert.Equal(0, fake.Calls); Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/iyzico/sessions/{CapabilityTokens.New()}")).StatusCode);
    }
    [Fact]
    public async Task Verified_failure_never_creates_a_staff_queue_entry_and_new_checkout_is_independent()
    {
        var fake = new Fake { Outcome = IyzicoOutcome.Failed }; using var host = await Host(fake); using var c = host.CreateClient(new() { AllowAutoRedirect = false }); var (payment, token) = await Create(c); (await Post(c, Init(token), Buyer())).EnsureSuccessStatusCode(); Assert.Equal(HttpStatusCode.Found, (await Callback(c, payment)).StatusCode);
        var result = await c.GetFromJsonAsync<JsonElement>($"/api/iyzico/sessions/{token}"); Assert.Equal("Failed", result.GetProperty("paymentStatus").GetString()); Assert.Equal(JsonValueKind.Null, result.GetProperty("trackingUrl").ValueKind);
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.False(await db.PaymentOutbox.IgnoreQueryFilters().AnyAsync(x => x.OrderId == payment.OrderId)); var (next, _) = await Create(c); Assert.NotEqual(payment.Id, next.Id); Assert.Equal(1, fake.Calls);
    }
}
