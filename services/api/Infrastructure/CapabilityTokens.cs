using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Kahve.Api.Infrastructure;

public sealed class CapabilityTokens(IConfiguration config)
{
    private readonly byte[] key = SHA256.HashData(Encoding.UTF8.GetBytes(Require(config["Tracking:EncryptionKey"])));
    private static string Require(string? secret) => secret is { Length: >= 32 } ? secret : throw new InvalidOperationException("Tracking__EncryptionKey must contain at least 32 characters.");
    public static string New() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static bool Valid(string token) => token.Length == 43 && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public string Protect(string token)
    {
        var plain = Encoding.UTF8.GetBytes(token);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        return WebEncoders.Base64UrlEncode([.. nonce, .. tag, .. cipher]);
    }
    public string Unprotect(string token)
    {
        var bytes = WebEncoders.Base64UrlDecode(token);
        var plain = new byte[bytes.Length - 28];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(28), bytes.AsSpan(12, 16), plain);
        return Encoding.UTF8.GetString(plain);
    }
}
