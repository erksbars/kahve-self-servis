using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Realtime;

namespace Kahve.Api.Modules.Orders;

public static class OrderTransitions
{
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["PaymentPending"] = ["Paid", "PaymentFailed", "Expired"],
        ["Paid"] = ["Received"], ["Received"] = ["Preparing"],
        ["Preparing"] = ["Ready"], ["Ready"] = ["Completed"]
    };
    public static bool CanChange(string status, string next) => Allowed.TryGetValue(status, out var targets) && targets.Contains(next);
    public static void Change(AppDbContext db, Order order, string next, string actor, string reason)
    {
        if (!CanChange(order.Status, next))
            throw new InvalidOperationException("Invalid order status transition.");
        db.OrderStatusHistory.Add(new() { TenantId = order.TenantId, OrderId = order.Id, FromStatus = order.Status, ToStatus = next, Actor = actor, Reason = reason, OccurredAt = DateTimeOffset.UtcNow });
        order.Status = next;
        order.Version++;
        db.RealtimeEvents.Add(new RealtimeEvent { Id = Guid.NewGuid(), TenantId = order.TenantId, BranchId = order.BranchId, OrderId = order.Id, Version = order.Version, CreatedAt = DateTimeOffset.UtcNow });
    }
}
