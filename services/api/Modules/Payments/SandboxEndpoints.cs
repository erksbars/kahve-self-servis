using System.Globalization;
using System.Text.Json;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Orders;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Payments;

public sealed record SignedWebhookEndpoint;
public sealed record SandboxDecision(string Outcome);
public static class SandboxEndpoints
{
    public static void MapSandboxEndpoints(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Sandbox:Enabled") || PaymentProviders.Selection(app.Configuration) == "none") return;
        app.MapPost("/api/payments/sandbox/webhook", async (HttpContext http, WebhookProcessor processor, IConfiguration config, CancellationToken ct) =>
        {
            if (!CheckoutEndpoints.Enabled(config)) return Results.NotFound();
            using var reader = new StreamReader(http.Request.Body);
            return await processor.Process(await reader.ReadToEndAsync(ct), http.Request.Headers["X-Sandbox-Timestamp"].ToString(), http.Request.Headers["X-Sandbox-Signature"].ToString(), ct);
        }).WithMetadata(new SignedWebhookEndpoint()).RequireRateLimiting("webhook");
        app.MapGet("/api/sandbox/sessions/{token}", Session).RequireRateLimiting("tracking");
        app.MapPost("/api/sandbox/sessions/{token}/decision", Decide).RequireRateLimiting("quote");
    }
    private static async Task<IResult> Session(string token, AppDbContext db, CapabilityTokens tokens, IConfiguration config, CancellationToken ct)
    {
        if (!CheckoutEndpoints.Enabled(config) || !CapabilityTokens.Valid(token)) return Results.NotFound();
        var hash = CapabilityTokens.Hash(token);
        var payment = await db.Payments.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Provider == "sandbox" && x.SessionTokenHash == hash, ct);
        if (payment is null) return Results.NotFound();
        var order = await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == payment.OrderId && x.TenantId == payment.TenantId, ct);
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(x => x.Id == order.BranchId && x.TenantId == order.TenantId, ct);
        var status = payment.Status == "Pending" && order.ExpiresAt <= DateTimeOffset.UtcNow ? "Expired" : payment.Status;
        return Results.Ok(new { amount = order.Total, order.Currency, paymentStatus = status, order.ExpiresAt, branch = new { branch.Name, branch.Slug }, items = JsonSerializer.Deserialize<List<QuotedItem>>(order.ItemsJson, CheckoutEndpoints.Json), trackingUrl = payment.Status == "Paid" ? $"/orders/track/{tokens.Unprotect(order.ProtectedTrackingToken)}" : null, demo = true });
    }
    private static async Task<IResult> Decide(string token, SandboxDecision decision, AppDbContext db, WebhookProcessor processor, SandboxPaymentProvider provider, CapabilityTokens tokens, IConfiguration config, CancellationToken ct)
    {
        if (!CheckoutEndpoints.Enabled(config) || !CapabilityTokens.Valid(token)) return Results.NotFound();
        if (decision.Outcome is not ("paid" or "failed")) return Results.BadRequest();
        var hash = CapabilityTokens.Hash(token);
        var payment = await db.Payments.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Provider == "sandbox" && x.SessionTokenHash == hash, ct);
        if (payment is null) return Results.NotFound();
        var body = JsonSerializer.Serialize(new SandboxWebhook($"decision-{payment.ProviderReference}", payment.ProviderReference, decision.Outcome, payment.Amount, payment.Currency), CheckoutEndpoints.Json);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        // The demo provider emits a server-signed event through the same verified processor as external webhooks.
        var result = await processor.Process(body, timestamp, provider.Sign(timestamp, body), ct);
        if (result is IStatusCodeHttpResult { StatusCode: >= 400 }) return result;
        db.ChangeTracker.Clear();
        return await Session(token, db, tokens, config, ct);
    }
}
