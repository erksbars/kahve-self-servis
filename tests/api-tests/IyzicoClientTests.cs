using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kahve.Api.Modules.Payments.Iyzico;
using Microsoft.Extensions.Configuration;

namespace Kahve.Api.Tests;
public sealed class IyzicoClientTests
{
    [Fact]
    public void Merchant_account_lookup_has_no_global_or_other_tenant_fallback()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [$"Iyzico:Merchants:{a:N}:ApiKey"] = "sandbox-a-key", [$"Iyzico:Merchants:{a:N}:SecretKey"] = "sandbox-a-secret", ["Iyzico:ApiKey"] = "sandbox-global-key", ["Iyzico:SecretKey"] = "sandbox-global-secret" }).Build();
        var accounts = new IyzicoMerchantAccounts(config); Assert.NotNull(accounts.Find(a)); Assert.Null(accounts.Find(b));
    }
    [Fact]
    public void Merchant_configuration_rejects_live_credentials()
    {
        var id = Guid.NewGuid(); var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [$"Iyzico:Merchants:{id:N}:ApiKey"] = "live-key", [$"Iyzico:Merchants:{id:N}:SecretKey"] = "live-secret" }).Build();
        Assert.Throws<InvalidOperationException>(() => new IyzicoMerchantAccounts(config).Find(id));
    }
    private static readonly IyzicoCredentials Credentials = new("test-api", "test-secret");
    private static string Hmac(string value) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("test-secret"), Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static IyzicoInitializeRequest Input() => new("conversation", "basket", 100.50m, new("https://coffee.example.test/callback"), new("buyer", "Test", "Buyer", "test-identity", "buyer@example.test", "+900000000000", "Test address", "Istanbul", "Turkey", "192.0.2.1"), new("Test address", "Test Buyer", "Istanbul", "Turkey"), new("Pickup test address", "Test Buyer", "Istanbul", "Turkey"), [new("item", "Coffee", "Coffee", 100.50m)]);
    private static IyzicoExpectedPayment Expected() => new("conversation", "basket", "provider-token", 100.50m);
    private static Dictionary<string, object?> Result(int? fraud = 1) => new()
    {
        ["status"] = "success", ["paymentStatus"] = "SUCCESS", ["paymentId"] = "payment-1", ["currency"] = "TRY", ["basketId"] = "basket", ["conversationId"] = "conversation", ["paidPrice"] = 100.50m, ["price"] = "100.500", ["token"] = "provider-token", ["signature"] = Hmac("SUCCESS:payment-1:TRY:basket:conversation:100.5:100.5:provider-token"), ["fraudStatus"] = fraud,
        ["itemTransactions"] = new[] { new { itemId = "item", paymentTransactionId = "transaction-1", price = 100.50m, paidPrice = 100.50m } }
    };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return await action(request, ct); }
    }
    private static HttpResponseMessage Reply(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    private static Handler Static(object value) => new((_, _) => Task.FromResult(Reply(value)));
    [Fact]
    public async Task Initialize_authenticates_exact_UTF8_body_and_returns_verified_hosted_session()
    {
        var oldCulture = CultureInfo.CurrentCulture; CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
        try
        {
            using var handler = new Handler(async (request, ct) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method); Assert.Equal("https://sandbox-api.iyzipay.com" + IyzicoCheckoutClient.InitializePath, request.RequestUri!.AbsoluteUri);
                var body = await request.Content!.ReadAsStringAsync(ct); var random = request.Headers.GetValues("x-iyzi-rnd").Single();
                var authorization = request.Headers.GetValues("Authorization").Single(); Assert.StartsWith("IYZWSv2 ", authorization);
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[8..])); Assert.Equal($"apiKey:test-api&randomKey:{random}&signature:{Hmac(random + IyzicoCheckoutClient.InitializePath + body)}", decoded);
                using var json = JsonDocument.Parse(body); Assert.Equal("100.5", json.RootElement.GetProperty("price").GetString()); Assert.Equal("Test address", json.RootElement.GetProperty("billingAddress").GetProperty("address").GetString()); Assert.False(json.RootElement.TryGetProperty("secretKey", out _)); Assert.Equal(1, json.RootElement.GetProperty("enabledInstallments")[0].GetInt32());
                return Reply(new { status = "success", conversationId = "conversation", token = "provider-token", paymentPageUrl = "https://sandbox-cpp.iyzipay.com/?token=provider-token", signature = Hmac("conversation:provider-token") });
            });
            using var http = new HttpClient(handler); var client = new IyzicoCheckoutClient(http); var session = await client.Initialize(Credentials, Input(), default);
            Assert.Equal("provider-token", session.Token); Assert.Equal("sandbox-cpp.iyzipay.com", session.PaymentPageUrl.Host); Assert.DoesNotContain("provider-token", session.ToString()); Assert.DoesNotContain("test-secret", Credentials.ToString()); Assert.Equal(1, handler.Calls);
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }
    }
    [Theory]
    [InlineData("http://sandbox-cpp.iyzipay.com")]
    [InlineData("https://sandbox-cpp.iyzipay.com.evil.test")]
    [InlineData("https://cpp.iyzipay.com")]
    [InlineData("https://user:pass@sandbox-cpp.iyzipay.com")]
    public async Task Initialize_rejects_unsafe_or_live_payment_redirect(string page)
    {
        using var handler = Static(new { status = "success", conversationId = "conversation", token = "provider-token", paymentPageUrl = page, signature = Hmac("conversation:provider-token") }); using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<IyzicoProtocolException>(() => new IyzicoCheckoutClient(http).Initialize(Credentials, Input(), default)); Assert.Equal("unsafe_payment_url", error.Code); Assert.True(error.MayHaveCreatedSession);
    }
    [Fact]
    public async Task Invalid_cart_or_callback_never_sends_a_request()
    {
        using var handler = Static(new { }); using var http = new HttpClient(handler); var client = new IyzicoCheckoutClient(http);
        await Assert.ThrowsAsync<ArgumentException>(() => client.Initialize(Credentials, Input() with { Amount = 200 }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Initialize(Credentials, Input() with { CallbackUrl = new("http://127.0.0.1/callback") }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Initialize(Credentials, Input() with { ShippingAddress = null }, default)); Assert.Equal(0, handler.Calls);
    }
    [Fact]
    public async Task Retrieve_verifies_signature_normalizes_decimal_strings_and_preserves_transaction_ids()
    {
        using var handler = Static(Result()); using var http = new HttpClient(handler); var result = await new IyzicoCheckoutClient(http).Retrieve(Credentials, Expected(), default);
        Assert.Equal(IyzicoOutcome.Confirmed, result.Outcome); Assert.Equal(100.50m, result.Amount); Assert.Equal("transaction-1", Assert.Single(result.Lines).TransactionId);
    }
    [Theory]
    [InlineData("signature", "0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("paidPrice", "99.99")]
    [InlineData("token", "other-token")]
    [InlineData("basketId", "other-basket")]
    [InlineData("currency", "USD")]
    public async Task Altered_payment_result_is_never_accepted(string field, string value)
    {
        var response = Result(); response[field] = value; using var handler = Static(response); using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<IyzicoProtocolException>(() => new IyzicoCheckoutClient(http).Retrieve(Credentials, Expected(), default));
    }
    [Fact]
    public async Task Signed_result_for_another_expected_order_is_rejected()
    {
        using var handler = Static(Result()); using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<IyzicoProtocolException>(() => new IyzicoCheckoutClient(http).Retrieve(Credentials, Expected() with { BasketId = "foreign-basket" }, default)); Assert.Equal("correlation_mismatch", error.Code);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Fraud_review_or_rejection_cannot_release_a_paid_order_to_operations(int fraud)
    {
        using var handler = Static(Result(fraud)); using var http = new HttpClient(handler); var result = await new IyzicoCheckoutClient(http).Retrieve(Credentials, Expected(), default); Assert.Equal(IyzicoOutcome.Review, result.Outcome);
    }
    [Fact]
    public async Task Missing_line_results_cannot_confirm_a_payment()
    {
        var response = Result(); response.Remove("itemTransactions"); using var handler = Static(response); using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<IyzicoProtocolException>(() => new IyzicoCheckoutClient(http).Retrieve(Credentials, Expected(), default)); Assert.Equal("invalid_line_results", error.Code);
    }
    [Fact]
    public async Task Explicit_initialization_rejection_has_safe_code_and_no_possible_session()
    {
        using var handler = Static(new { status = "failure", errorCode = "5", errorMessage = "Private provider message" }); using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<IyzicoProtocolException>(() => new IyzicoCheckoutClient(http).Initialize(Credentials, Input(), default));
        Assert.Equal("provider_rejected_5", error.Code); Assert.False(error.MayHaveCreatedSession); Assert.DoesNotContain("Private", error.Message);
    }
    [Fact]
    public async Task Initialization_timeout_is_marked_ambiguous_and_is_not_retried()
    {
        using var handler = new Handler((_, _) => throw new TaskCanceledException("Test timeout")); using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<IyzicoProtocolException>(() => new IyzicoCheckoutClient(http).Initialize(Credentials, Input(), default)); Assert.True(error.MayHaveCreatedSession); Assert.Equal(1, handler.Calls); Assert.DoesNotContain("Test timeout", error.Message);
    }
}
