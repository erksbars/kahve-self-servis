using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Orders;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Payments;

// Never bind this type to an HTTP body. A provider adapter creates it after authenticating
// the notification (and retrieving provider status where its protocol requires that).
internal sealed record VerifiedPaymentEvent(string Provider, string EventId, string ProviderReference, string Status, decimal Amount, string Currency, string PayloadHash, bool RejectAfterExpiry);

public sealed class PaymentSettlement(AppDbContext db)
{
    internal async Task<IResult> Apply(VerifiedPaymentEvent input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await DatabaseLocks.Acquire(db, $"webhook:{input.Provider}:{input.EventId}", ct);
        await DatabaseLocks.Acquire(db, $"payment:{input.Provider}:{input.ProviderReference}", ct);
        var previous = await db.PaymentWebhookEvents.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Provider == input.Provider && x.EventId == input.EventId, ct);
        if (previous is not null) return previous.PayloadHash == input.PayloadHash ? Results.Ok(new { duplicate = true }) : Results.Conflict(new { message = "Webhook kimliği farklı içerikle tekrar kullanılamaz." });
        var payment = await db.Payments.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Provider == input.Provider && x.ProviderReference == input.ProviderReference, ct);
        if (payment is null) return Results.NotFound();
        await DatabaseLocks.Acquire(db, $"order:{payment.OrderId}", ct);
        await db.Entry(payment).ReloadAsync(ct);
        var order = await db.Orders.IgnoreQueryFilters().SingleAsync(x => x.Id == payment.OrderId && x.TenantId == payment.TenantId && x.BranchId == payment.BranchId, ct);
        if (input.Amount != payment.Amount || input.Currency != payment.Currency) return Results.BadRequest(new { message = "Ödeme tutarı veya para birimi eşleşmiyor." });
        // Sandbox expiry is a simulation policy. A real adapter must explicitly decide
        // how late confirmed charges are reconciled; it must not discard a real charge.
        if (input.RejectAfterExpiry && payment.Status == "Pending" && order.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            payment.Status = order.PaymentStatus = "Expired";
            OrderTransitions.Change(db, order, "Expired", "system", "checkout.expired");
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.Json(new { message = "Ödeme oturumunun süresi doldu." }, statusCode: 410);
        }
        if (payment.Status != "Pending" && !(payment.Status == "Paid" && input.Status == "paid") && !(payment.Status == "Failed" && input.Status == "failed"))
            return Results.Conflict(new { message = "Tamamlanmış ödeme sonucu değiştirilemez." });
        var now = DateTimeOffset.UtcNow;
        db.PaymentWebhookEvents.Add(new() { TenantId = order.TenantId, OrderId = order.Id, Provider = input.Provider, EventId = input.EventId, PayloadHash = input.PayloadHash, ReceivedAt = now });
        if (payment.Status == "Pending")
        {
            payment.Status = order.PaymentStatus = input.Status == "paid" ? "Paid" : "Failed";
            OrderTransitions.Change(db, order, input.Status == "paid" ? "Paid" : "PaymentFailed", input.Provider, "payment.verified");
            if (payment.Status == "Paid")
            {
                order.PaidAt = now;
                db.PaymentOutbox.Add(new() { Id = Guid.NewGuid(), TenantId = order.TenantId, OrderId = order.Id, CreatedAt = now, NextAttemptAt = now });
            }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Results.Ok(new { accepted = true });
    }
}
