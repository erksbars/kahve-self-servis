using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Payments;
using Microsoft.Extensions.Configuration;

namespace Kahve.Api.Tests;
public sealed class PaymentProviderTests
{
    private static IConfiguration Config(string? provider = null, bool sandbox = true, string? secret = "Only-for-provider-tests-at-least-32-characters") => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Payments:Provider"] = provider, ["Sandbox:Enabled"] = sandbox.ToString(), ["Payments:WebhookSecret"] = secret }).Build();
    [Theory]
    [InlineData("iyzico")]
    [InlineData("paytr")]
    [InlineData("Sandbox")]
    public void Uninstalled_or_mistyped_adapter_never_silently_falls_back(string selection)
    {
        var config = Config(selection);
        Assert.Throws<InvalidOperationException>(() => new PaymentProviders(config, [new SandboxPaymentProvider(config)]));
    }
    [Fact]
    public void No_payment_mode_does_not_require_secrets_even_if_demo_flag_is_present()
    {
        var config = Config("none", secret: null);
        var registry = new PaymentProviders(config, [new SandboxPaymentProvider(config)]);
        Assert.Equal("none", registry.SelectedName); Assert.False(CheckoutEndpoints.Enabled(config));
        Assert.Throws<InvalidOperationException>(() => registry.Selected);
    }
    [Fact]
    public void Active_sandbox_requires_its_flag_and_signing_secret()
    {
        var withoutFlag = Config("sandbox", sandbox: false);
        Assert.Throws<InvalidOperationException>(() => new PaymentProviders(withoutFlag, [new SandboxPaymentProvider(withoutFlag)]));
        var withoutSecret = Config("sandbox", secret: null);
        Assert.Throws<InvalidOperationException>(() => new PaymentProviders(withoutSecret, [new SandboxPaymentProvider(withoutSecret)]));
    }
    [Fact]
    public void Sandbox_continuation_accepts_only_a_full_session_capability()
    {
        var provider = new SandboxPaymentProvider(Config());
        var token = CapabilityTokens.New(); Assert.Equal($"/sandbox/pay/{token}", provider.CheckoutPath(token));
        Assert.Throws<ArgumentException>(() => provider.CheckoutPath("https://external.invalid"));
        Assert.Throws<ArgumentException>(() => provider.CheckoutPath("short"));
    }
}
