using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Orders;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Payments;

public interface IOrderReceiver { void Receive(AppDbContext db, Order order); }
public sealed class OrderReceiver : IOrderReceiver
{
    public void Receive(AppDbContext db, Order order)
    {
        if (order.PaymentStatus != "Paid") throw new InvalidOperationException("Only paid orders can enter operations.");
        if (order.Status == "Paid") OrderTransitions.Change(db, order, "Received", "outbox", "payment.confirmed");
        else if (order.Status is not ("Received" or "Preparing" or "Ready" or "Completed")) throw new InvalidOperationException("Unexpected operation status.");
    }
}
public sealed class OutboxPump(AppDbContext db, IOrderReceiver receiver)
{
    public async Task<bool> ProcessOne(CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.PaymentOutbox.FromSqlInterpolated($"SELECT * FROM \"PaymentOutbox\" WHERE \"ProcessedAt\" IS NULL AND \"NextAttemptAt\" <= {DateTimeOffset.UtcNow} ORDER BY \"CreatedAt\" LIMIT 1 FOR UPDATE SKIP LOCKED").IgnoreQueryFilters().ToListAsync(ct);
        var entry = rows.SingleOrDefault();
        if (entry is null) return false;
        try
        {
            await DatabaseLocks.Acquire(db, $"order:{entry.OrderId}", ct);
            var order = await db.Orders.IgnoreQueryFilters().SingleAsync(x => x.Id == entry.OrderId && x.TenantId == entry.TenantId, ct);
            receiver.Receive(db, order);
            entry.Attempts++;
            entry.ProcessedAt = DateTimeOffset.UtcNow;
            entry.LastError = null;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return true;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            await tx.RollbackAsync(ct);
            await tx.DisposeAsync();
            db.ChangeTracker.Clear();
            var next = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, 2 * Math.Pow(2, Math.Min(entry.Attempts, 5))));
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"PaymentOutbox\" SET \"Attempts\" = \"Attempts\" + 1, \"LastError\" = 'dispatch_failed', \"NextAttemptAt\" = {next} WHERE \"Id\" = {entry.Id} AND \"ProcessedAt\" IS NULL", ct);
            return false;
        }
    }
    public async Task ExpireBatch(CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var ids = await db.Orders.IgnoreQueryFilters().AsNoTracking().Where(x => x.PaymentStatus == "Pending" && x.ExpiresAt <= DateTimeOffset.UtcNow).Select(x => x.Id).Take(20).ToListAsync(ct);
        foreach (var id in ids)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await DatabaseLocks.Acquire(db, $"order:{id}", ct);
            var order = await db.Orders.IgnoreQueryFilters().SingleAsync(x => x.Id == id, ct);
            if (order.PaymentStatus != "Pending" || order.ExpiresAt > DateTimeOffset.UtcNow) continue;
            var payment = await db.Payments.IgnoreQueryFilters().SingleAsync(x => x.OrderId == id && x.TenantId == order.TenantId, ct);
            order.PaymentStatus = payment.Status = "Expired";
            OrderTransitions.Change(db, order, "Expired", "system", "checkout.expired");
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
    }
}
public sealed class PaymentOutboxWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<PaymentOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue<bool>("Sandbox:Enabled") || !config.GetValue("Outbox:RunWorker", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var pump = scope.ServiceProvider.GetRequiredService<OutboxPump>();
                for (var i = 0; i < 20 && await pump.ProcessOne(ct); i++) { }
                await pump.ExpireBatch(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception e) { logger.LogWarning(e, "Payment outbox iteration failed; stored work will be retried."); }
        }
    }
}
