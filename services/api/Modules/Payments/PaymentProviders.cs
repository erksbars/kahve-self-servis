namespace Kahve.Api.Modules.Payments;

public sealed class PaymentProviders
{
    private readonly IReadOnlyDictionary<string, IPaymentProvider> registered;
    public string SelectedName { get; }
    public IPaymentProvider Selected => Resolve(SelectedName) ?? throw new InvalidOperationException("No checkout provider is enabled.");
    public static string Selection(IConfiguration config) => config["Payments:Provider"] ?? (config.GetValue<bool>("Sandbox:Enabled") ? "sandbox" : "none");

    public PaymentProviders(IConfiguration config, IEnumerable<IPaymentProvider> providers)
    {
        registered = providers.ToDictionary(x => x.Name, StringComparer.Ordinal);
        SelectedName = Selection(config);
        if (SelectedName == "none") return;
        var selected = Resolve(SelectedName) ?? throw new InvalidOperationException("Payments__Provider has no installed adapter.");
        if (SelectedName == "sandbox" && !config.GetValue<bool>("Sandbox:Enabled")) throw new InvalidOperationException("The sandbox provider requires Sandbox__Enabled=true.");
        selected.ValidateConfiguration();
    }
    // Idempotent retries use the provider persisted on the payment, never today's selection.
    public IPaymentProvider? Resolve(string name) => registered.GetValueOrDefault(name);
}
