using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Kahve.Api.Modules.Payments;

public interface IPaymentProvider
{
    string Name { get; }
    string CreateReference();
    // A local continuation route only; remote initialization belongs outside a DB transaction.
    string CheckoutPath(string sessionToken);
    void ValidateConfiguration();
}

public sealed class SandboxPaymentProvider(IConfiguration config) : IPaymentProvider
{
    private byte[] SigningKey => Encoding.UTF8.GetBytes(config["Payments:WebhookSecret"] is { Length: >= 32 } secret ? secret : throw new InvalidOperationException("Payments__WebhookSecret must contain at least 32 characters."));
    public string Name => "sandbox";
    public string CreateReference() => Guid.NewGuid().ToString("N");
    public string CheckoutPath(string sessionToken)
    {
        if (!Infrastructure.CapabilityTokens.Valid(sessionToken)) throw new ArgumentException("Invalid session capability.", nameof(sessionToken));
        return $"/sandbox/pay/{sessionToken}";
    }
    public void ValidateConfiguration() => _ = SigningKey;
    public string Sign(string timestamp, string body) => Convert.ToHexString(HMACSHA256.HashData(SigningKey, Encoding.UTF8.GetBytes($"{timestamp}.{body}")));
    public bool Verify(string timestamp, string body, string signature)
    {
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)) return false;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (seconds < now - 300 || seconds > now + 300 || signature.Length != 64) return false;
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Sign(timestamp, body)), Convert.FromHexString(signature)); }
        catch (FormatException) { return false; }
    }
}
