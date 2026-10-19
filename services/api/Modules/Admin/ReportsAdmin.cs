using Kahve.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Admin;
public static partial class AdminEndpoints
{
    private static void MapReports(RouteGroupBuilder group) => group.MapGet("/reports/summary", async (DateOnly from, DateOnly to, Guid? branchId, AppDbContext db, CancellationToken ct) =>
    {
        if (from > to || to.DayNumber - from.DayNumber > 365 || to == DateOnly.MaxValue) return Invalid("Tarih aralığı en fazla 366 gün olabilir.");
        var branch = branchId.HasValue ? await db.Branches.SingleOrDefaultAsync(x => x.Id == branchId, ct) : null;
        if (branchId.HasValue && branch is null) return Results.NotFound();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(branch?.TimeZone ?? "Europe/Istanbul");
        var localStart = from.ToDateTime(TimeOnly.MinValue);
        var localEnd = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        if (zone.IsInvalidTime(localStart) || zone.IsInvalidTime(localEnd)) return Invalid("Bu saat diliminde tarih sınırı saat değişimine denk geliyor. Farklı bir tarih aralığı seçin.");
        var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, zone));
        var end = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, zone));
        var query = db.Orders.AsNoTracking().Where(x => !branchId.HasValue || x.BranchId == branchId);
        var paid = await query.Where(x => x.PaymentStatus == "Paid" && x.PaidAt >= start && x.PaidAt < end).Select(x => new { x.Id, x.BranchId, x.Total, x.Status }).ToListAsync(ct);
        var ids = paid.Select(x => x.Id).ToList();
        var history = await db.OrderStatusHistory.AsNoTracking().Where(x => ids.Contains(x.OrderId) && (x.ToStatus == "Preparing" || x.ToStatus == "Ready")).ToListAsync(ct);
        var preparation = history.GroupBy(x => x.OrderId).Select(g => new { preparing = g.Where(x => x.ToStatus == "Preparing").Min(x => (DateTimeOffset?)x.OccurredAt), ready = g.Where(x => x.ToStatus == "Ready").Min(x => (DateTimeOffset?)x.OccurredAt) }).Where(x => x.preparing.HasValue && x.ready.HasValue && x.ready >= x.preparing).Select(x => (x.ready!.Value - x.preparing!.Value).TotalMinutes).ToList();
        var cancelled = await query.CountAsync(x => x.Status == "Cancelled" && x.CreatedAt >= start && x.CreatedAt < end, ct);
        var failed = await query.CountAsync(x => x.PaymentStatus == "Failed" && x.CreatedAt >= start && x.CreatedAt < end, ct);
        return Results.Ok(new { from, to, timeZone = zone.Id, currency = "TRY", paidOrders = paid.Count, completedOrders = paid.Count(x => x.Status == "Completed"), revenue = paid.Sum(x => x.Total), cancelledOrders = cancelled, failedPayments = failed, averagePreparationMinutes = preparation.Count > 0 ? (double?)Math.Round(preparation.Average(), 1) : null, preparationSamples = preparation.Count, demo = true });
    });
}
