using Kahve.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Orders;
public sealed record StatusRequest(string Status, int ExpectedVersion);
public static class StaffOrderEndpoints
{
    public static void MapStaffOrderEndpoints(this WebApplication app) => app.MapPatch("/api/staff/orders/{id:guid}/status", Update).RequireAuthorization();
    private static async Task<IResult> Update(Guid id, StatusRequest input, AppDbContext db, RequestScope scope, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await DatabaseLocks.Acquire(db, $"order:{id}", ct);
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == id && db.StaffBranchAccess.Any(a => a.StaffId == scope.StaffId && a.BranchId == o.BranchId), ct);
        if (order is null) return Results.NotFound();
        if (input.ExpectedVersion != order.Version) return Results.Conflict(new { code = "version_conflict", message = "Sipariş başka bir personel tarafından güncellendi. Güncel durumu kontrol edin.", current = new { order.Status, order.Version } });
        if (order.PaymentStatus != "Paid" || input.Status is not ("Preparing" or "Ready" or "Completed") || !OrderTransitions.CanChange(order.Status, input.Status))
            return Results.Conflict(new { code = "invalid_transition", message = "Bu sipariş için durum geçişine izin verilmiyor.", current = new { order.Status, order.Version } });
        OrderTransitions.Change(db, order, input.Status, $"staff:{scope.StaffId}", $"staff.{input.Status.ToLowerInvariant()}");
        db.AuditLogs.Add(new() { TenantId = order.TenantId, ActorId = scope.StaffId!.Value, EntityId = order.Id, Action = $"order.{input.Status.ToLowerInvariant()}", OccurredAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Results.Ok(new { order.Id, order.Status, order.Version });
    }
}
