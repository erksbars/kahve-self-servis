using Kahve.Api.Infrastructure;

namespace Kahve.Api.Modules.Payments.Iyzico;
public sealed class IyzicoPaymentProvider(IConfiguration config) : IPaymentProvider
{
    public string Name => "iyzico";
    public string CreateReference() => Guid.NewGuid().ToString("N");
    public string CheckoutPath(string token)
    {
        if (!CapabilityTokens.Valid(token)) throw new ArgumentException("Invalid checkout capability.");
        return $"/iyzico/pay/{token}";
    }
    public void ValidateConfiguration()
    {
        if (!Uri.TryCreate(config["Iyzico:CallbackOrigin"], UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.IsLoopback || uri.UserInfo != "" || uri.AbsolutePath != "/" || uri.Query != "" || uri.Fragment != "") throw new InvalidOperationException("Iyzico__CallbackOrigin must be a public HTTPS origin.");
    }
}
