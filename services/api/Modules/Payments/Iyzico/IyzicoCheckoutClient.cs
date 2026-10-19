using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kahve.Api.Modules.Payments.Iyzico;

// Sandbox transport only. Credentials are supplied per call so account ownership is
// resolved by the application, rather than taken from a browser or shared across tenants.
public sealed class IyzicoCheckoutClient(HttpClient http) : IIyzicoCheckoutClient
{
    public const string InitializePath = "/payment/iyzipos/checkoutform/initialize/auth/ecom";
    public const string RetrievePath = "/payment/iyzipos/checkoutform/auth/ecom/detail";
    private static readonly Uri Sandbox = new("https://sandbox-api.iyzipay.com");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private static string Money(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    private static bool Price(decimal value) => value >= 0 && value <= 9999999999.99m && decimal.Round(value, 2) == value;
    private static bool Text(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 500;

    public async Task<IyzicoSession> Initialize(IyzicoCredentials credentials, IyzicoInitializeRequest input, CancellationToken ct)
    {
        Validate(input);
        var payload = new { locale = "tr", input.ConversationId, input.BasketId, price = Money(input.Amount), paidPrice = Money(input.Amount), currency = "TRY", paymentGroup = "PRODUCT", callbackUrl = input.CallbackUrl.AbsoluteUri, enabledInstallments = new[] { 1 }, input.Buyer, input.BillingAddress, input.ShippingAddress, basketItems = input.Items.Select(x => new { x.Id, x.Name, x.Category1, price = Money(x.Price), x.ItemType, x.SubMerchantKey, subMerchantPrice = x.SubMerchantPrice.HasValue ? Money(x.SubMerchantPrice.Value) : null }) };
        using var reply = await Post(credentials, InitializePath, payload, true, ct);
        var root = reply.RootElement;
        try
        {
            RequireSuccess(root);
            var conversation = String(root, "conversationId"); var token = String(root, "token");
            if (conversation != input.ConversationId || !Text(token)) throw new IyzicoProtocolException("correlation_mismatch", true);
            Verify(credentials, root, [conversation, token]);
            if (!Uri.TryCreate(String(root, "paymentPageUrl"), UriKind.Absolute, out var page) || page.Scheme != "https" || page.Host != "sandbox-cpp.iyzipay.com" || !page.IsDefaultPort || page.UserInfo != "") throw new IyzicoProtocolException("unsafe_payment_url", true);
            return new(token, page);
        }
        catch (IyzicoProtocolException e) { throw new IyzicoProtocolException(e.Code, !e.Code.StartsWith("provider_rejected_", StringComparison.Ordinal)); }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or FormatException) { throw new IyzicoProtocolException("invalid_response", true); }
    }

    public async Task<IyzicoResult> Retrieve(IyzicoCredentials credentials, IyzicoExpectedPayment expected, CancellationToken ct)
    {
        if (!Text(expected.ConversationId) || !Text(expected.BasketId) || !Text(expected.Token) || !Price(expected.Amount) || expected.Amount <= 0) throw new ArgumentException("Invalid payment expectation.");
        using var reply = await Post(credentials, RetrievePath, new { locale = "tr", expected.ConversationId, expected.Token }, false, ct);
        var root = reply.RootElement;
        try
        {
            RequireSuccess(root);
            var status = String(root, "paymentStatus"); var paymentId = String(root, "paymentId"); var currency = String(root, "currency"); var basket = String(root, "basketId"); var conversation = String(root, "conversationId"); var token = String(root, "token");
            var price = Decimal(root.GetProperty("price")); var paid = Decimal(root.GetProperty("paidPrice"));
            Verify(credentials, root, [status, paymentId, currency, basket, conversation, Money(paid), Money(price), token]);
            if (conversation != expected.ConversationId || basket != expected.BasketId || token != expected.Token || !Text(paymentId)) throw new IyzicoProtocolException("correlation_mismatch");
            if (currency != "TRY" || price != expected.Amount || (status == "SUCCESS" && paid != expected.Amount) || (status == "FAILURE" && paid != 0)) throw new IyzicoProtocolException("amount_mismatch");
            if (status is not ("SUCCESS" or "FAILURE")) throw new IyzicoProtocolException("unrecognized_status");
            var fraud = root.TryGetProperty("fraudStatus", out var fraudValue) && fraudValue.TryGetInt32(out var f) ? f : 0;
            var outcome = status == "FAILURE" ? IyzicoOutcome.Failed : fraud == 1 ? IyzicoOutcome.Confirmed : IyzicoOutcome.Review;
            var lines = root.TryGetProperty("itemTransactions", out var transactions) && transactions.ValueKind == JsonValueKind.Array ? transactions.EnumerateArray().Select(x => new IyzicoLineResult(String(x, "itemId"), String(x, "paymentTransactionId"), Decimal(x.GetProperty("price")), Decimal(x.GetProperty("paidPrice")))).ToList() : [];
            if (outcome == IyzicoOutcome.Confirmed && (lines.Count == 0 || lines.Any(x => !Text(x.ItemId) || !Text(x.TransactionId) || x.Price < 0 || x.PaidPrice < 0) || lines.Sum(x => x.Price) != expected.Amount || lines.Sum(x => x.PaidPrice) != paid)) throw new IyzicoProtocolException("invalid_line_results");
            return new(paymentId, outcome, paid, currency, lines);
        }
        catch (IyzicoProtocolException) { throw; }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or FormatException or OverflowException) { throw new IyzicoProtocolException("invalid_response"); }
    }

    private async Task<JsonDocument> Post(IyzicoCredentials credentials, string path, object payload, bool initialization, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(payload, Json); var random = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var digest = Hmac(credentials, random + path + body);
        var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes($"apiKey:{credentials.ApiKey}&randomKey:{random}&signature:{digest}"));
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Sandbox, path)) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("Authorization", "IYZWSv2 " + authorization); request.Headers.Add("x-iyzi-rnd", random);
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) throw new IyzicoProtocolException("http_error", initialization);
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var memory = new MemoryStream(); var buffer = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(buffer, ct)) > 0) { if (memory.Length + count > 1024 * 1024) throw new IyzicoProtocolException("response_too_large", initialization); memory.Write(buffer, 0, count); }
            return JsonDocument.Parse(memory.ToArray());
        }
        catch (IyzicoProtocolException) { throw; }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException or IOException) { throw new IyzicoProtocolException("transport_or_response_error", initialization); }
    }
    private static string Hmac(IyzicoCredentials credentials, string value) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(credentials.SecretKey), Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static void Verify(IyzicoCredentials credentials, JsonElement root, string[] values)
    {
        var signature = String(root, "signature");
        if (signature.Length != 64) throw new IyzicoProtocolException("invalid_signature");
        try { if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Hmac(credentials, string.Join(':', values))), Convert.FromHexString(signature))) throw new IyzicoProtocolException("invalid_signature"); }
        catch (FormatException) { throw new IyzicoProtocolException("invalid_signature"); }
    }
    private static void RequireSuccess(JsonElement root)
    {
        var status = String(root, "status");
        if (status == "success") return;
        if (status != "failure") throw new IyzicoProtocolException("invalid_response");
        var code = root.TryGetProperty("errorCode", out var value) ? value.ToString() : "";
        if (code.Length is < 1 or > 12 || !code.All(char.IsAsciiDigit)) code = "unspecified";
        throw new IyzicoProtocolException("provider_rejected_" + code);
    }
    private static string String(JsonElement element, string name) => element.GetProperty(name).GetString() ?? "";
    private static decimal Decimal(JsonElement value) => value.ValueKind == JsonValueKind.String ? decimal.Parse(value.GetString()!, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture) : value.GetDecimal();
    private static void Validate(IyzicoInitializeRequest x)
    {
        if (!Text(x.ConversationId) || !Text(x.BasketId) || !Price(x.Amount) || x.Amount <= 0 || x.CallbackUrl.Scheme != "https" || x.CallbackUrl.IsLoopback || x.CallbackUrl.UserInfo != "" || x.CallbackUrl.Fragment != "" || x.Items.Count is < 1 or > 20 || x.Items.Any(i => !Text(i.Id) || !Text(i.Name) || !Text(i.Category1) || !Price(i.Price) || i.ItemType is not ("PHYSICAL" or "VIRTUAL") || (i.SubMerchantKey is null) != (i.SubMerchantPrice is null) || (i.SubMerchantPrice.HasValue && (!Text(i.SubMerchantKey) || !Price(i.SubMerchantPrice.Value) || i.SubMerchantPrice.Value > i.Price))) || x.Items.Select(i => i.Id).Distinct().Count() != x.Items.Count || x.Items.Sum(i => i.Price) != x.Amount || (x.Items.Any(i => i.ItemType == "PHYSICAL") && x.ShippingAddress is null)) throw new ArgumentException("Invalid iyzico checkout request.");
        var buyer = x.Buyer;
        if (new[] { buyer.Id, buyer.Name, buyer.Surname, buyer.IdentityNumber, buyer.Email, buyer.GsmNumber, buyer.RegistrationAddress, buyer.City, buyer.Country }.Any(s => !Text(s)) || !System.Net.Mail.MailAddress.TryCreate(buyer.Email, out _) || !IPAddress.TryParse(buyer.Ip, out _)) throw new ArgumentException("Required buyer information is missing.");
        foreach (var address in new[] { x.BillingAddress, x.ShippingAddress }.Where(a => a is not null)) if (new[] { address!.Address, address.ContactName, address.City, address.Country }.Any(s => !Text(s))) throw new ArgumentException("Required address information is missing.");
    }
}
