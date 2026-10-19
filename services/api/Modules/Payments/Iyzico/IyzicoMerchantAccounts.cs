using Kahve.Api.Infrastructure;

namespace Kahve.Api.Modules.Payments.Iyzico;

// Merchant account routing is always determined by the order's persisted TenantId.
// There is deliberately no global/default account fallback.
public sealed class IyzicoMerchantAccounts(IConfiguration config)
{
    public IyzicoCredentials? Find(Guid tenantId)
    {
        var section = config.GetSection($"Iyzico:Merchants:{tenantId:N}");
        var key = section["ApiKey"]; var secret = section["SecretKey"];
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret)) return null;
        if (!key.StartsWith("sandbox-", StringComparison.Ordinal) || !secret.StartsWith("sandbox-", StringComparison.Ordinal)) throw new InvalidOperationException("Only iyzico sandbox credentials are accepted in this integration stage.");
        return new(key, secret);
    }
    public static void MapReadiness(WebApplication app)
    {
        app.MapGet("/api/admin/payments/iyzico", (RequestScope scope, IyzicoMerchantAccounts accounts, IConfiguration config) =>
        {
            var configured = false; var valid = true;
            try { configured = accounts.Find(scope.TenantId!.Value) is not null; }
            catch (InvalidOperationException) { valid = false; }
            catch (ArgumentException) { valid = false; }
            return Results.Ok(new { tenantId = scope.TenantId, provider = "iyzico", environment = "sandbox", accountModel = "perMerchant", credentialsConfigured = configured, configurationValid = valid, checkoutConnected = configured && valid && PaymentProviders.Selection(config) == "iyzico" });
        }).RequireAuthorization(p => p.RequireRole("Owner"));
    }
}
