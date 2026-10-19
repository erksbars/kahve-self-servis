using System.Text.Json;
using Kahve.Api.Infrastructure;

namespace Kahve.Api.Modules.Payments;

public sealed record SandboxWebhook(string EventId, string ProviderReference, string Status, decimal Amount, string Currency);
// This adapter owns sandbox signature/JSON validation. Only verified events reach settlement.
public sealed class WebhookProcessor(SandboxPaymentProvider provider, PaymentSettlement settlement)
{
    public async Task<IResult> Process(string body, string timestamp, string signature, CancellationToken ct)
    {
        if (!provider.Verify(timestamp, body, signature)) return Results.Json(new { message = "Webhook imzası veya zaman damgası geçersiz." }, statusCode: 401);
        SandboxWebhook? input;
        try { input = JsonSerializer.Deserialize<SandboxWebhook>(body, CheckoutEndpoints.Json); }
        catch (JsonException) { return Results.BadRequest(); }
        if (input is null || string.IsNullOrWhiteSpace(input.EventId) || input.EventId.Length > 100 || string.IsNullOrWhiteSpace(input.ProviderReference) || input.ProviderReference.Length > 100 || input.Status is not ("paid" or "failed")) return Results.BadRequest();
        return await settlement.Apply(new(provider.Name, input.EventId, input.ProviderReference, input.Status, input.Amount, input.Currency, CapabilityTokens.Hash(body), RejectAfterExpiry: true), ct);
    }
}
