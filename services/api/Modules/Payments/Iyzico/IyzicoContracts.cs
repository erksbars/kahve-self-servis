namespace Kahve.Api.Modules.Payments.Iyzico;
public interface IIyzicoCheckoutClient
{
    Task<IyzicoSession> Initialize(IyzicoCredentials credentials, IyzicoInitializeRequest input, CancellationToken ct);
    Task<IyzicoResult> Retrieve(IyzicoCredentials credentials, IyzicoExpectedPayment expected, CancellationToken ct);
}

public sealed class IyzicoCredentials
{
    internal string ApiKey { get; }
    internal string SecretKey { get; }
    public IyzicoCredentials(string apiKey, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secretKey) || apiKey.Contains('&') || apiKey.Contains(':')) throw new ArgumentException("Invalid iyzico credentials.");
        ApiKey = apiKey; SecretKey = secretKey;
    }
    public override string ToString() => "IyzicoCredentials [redacted]";
}
public sealed record IyzicoBuyer(string Id, string Name, string Surname, string IdentityNumber, string Email, string GsmNumber, string RegistrationAddress, string City, string Country, string Ip);
public sealed record IyzicoAddress(string Address, string ContactName, string City, string Country);
public sealed record IyzicoBasketItem(string Id, string Name, string Category1, decimal Price, string ItemType = "PHYSICAL", string? SubMerchantKey = null, decimal? SubMerchantPrice = null);
public sealed record IyzicoInitializeRequest(string ConversationId, string BasketId, decimal Amount, Uri CallbackUrl, IyzicoBuyer Buyer, IyzicoAddress BillingAddress, IyzicoAddress? ShippingAddress, IReadOnlyList<IyzicoBasketItem> Items);
public sealed class IyzicoSession(string token, Uri paymentPageUrl)
{
    public string Token { get; } = token;
    public Uri PaymentPageUrl { get; } = paymentPageUrl;
    public override string ToString() => "IyzicoSession [redacted]";
}
public sealed record IyzicoExpectedPayment(string ConversationId, string BasketId, string Token, decimal Amount);
public enum IyzicoOutcome { Confirmed, Failed, Review }
public sealed record IyzicoLineResult(string ItemId, string TransactionId, decimal Price, decimal PaidPrice);
public sealed record IyzicoResult(string PaymentId, IyzicoOutcome Outcome, decimal Amount, string Currency, IReadOnlyList<IyzicoLineResult> Lines);
public sealed class IyzicoProtocolException(string code, bool mayHaveCreatedSession = false) : Exception("iyzico response could not be verified.")
{
    public string Code { get; } = code;
    public bool MayHaveCreatedSession { get; } = mayHaveCreatedSession;
}
