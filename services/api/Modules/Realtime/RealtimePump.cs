using Kahve.Api.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Realtime;
public sealed class RealtimePump(AppDbContext db, IHubContext<OrderHub> hub, StaffSubscriptions subscriptions)
{
    public async Task<bool> ProcessOne(CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.RealtimeEvents.FromSqlInterpolated($"SELECT * FROM \"RealtimeEvents\" WHERE \"ProcessedAt\" IS NULL ORDER BY \"CreatedAt\" LIMIT 1 FOR UPDATE SKIP LOCKED").IgnoreQueryFilters().ToListAsync(ct);
        var entry = rows.SingleOrDefault();
        if (entry is null) return false;
        foreach (var (connection, subscription) in subscriptions.Connections.Where(x => x.Value.BranchId == entry.BranchId).ToArray())
        {
            var allowed = await db.StaffUsers.IgnoreQueryFilters().AnyAsync(x => x.Id == subscription.StaffId && x.TenantId == subscription.TenantId && x.Active && x.Role == subscription.Role, ct)
                && await db.StaffBranchAccess.IgnoreQueryFilters().AnyAsync(x => x.StaffId == subscription.StaffId && x.TenantId == entry.TenantId && x.TenantId == subscription.TenantId && x.BranchId == entry.BranchId, ct);
            if (!allowed)
            {
                await hub.Groups.RemoveFromGroupAsync(connection, OrderHub.StaffGroup(subscription.BranchId), ct);
                var removed = ((ICollection<KeyValuePair<string, StaffSubscription>>)subscriptions.Connections).Remove(new(connection, subscription));
                if (removed) await hub.Clients.Client(connection).SendAsync("AccessRevoked", ct);
            }
        }
        if (entry.OrderId is { } order)
        {
            await hub.Clients.Group(OrderHub.TrackingGroup(order)).SendAsync("OrderChanged", new { entry.Version }, ct);
            await hub.Clients.Group(OrderHub.StaffGroup(entry.BranchId)).SendAsync("OrderChanged", new { entry.OrderId, entry.Version }, ct);
        }
        else await hub.Clients.Group(OrderHub.StaffGroup(entry.BranchId)).SendAsync("BranchChanged", new { entry.Version }, ct);
        entry.ProcessedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return true;
    }
}
public sealed class RealtimeWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<RealtimeWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue("Realtime:RunWorker", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(300));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var pump = scope.ServiceProvider.GetRequiredService<RealtimePump>();
                for (var i = 0; i < 50 && await pump.ProcessOne(ct); i++) { }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception e) { logger.LogWarning(e, "Realtime dispatch failed; event remains pending for retry."); }
        }
    }
}
