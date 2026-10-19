using Kahve.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Music;

public sealed record RequestSongDecision(string Status, int ExpectedVersion);
public static class OrderSongRequests
{
    public static void Map(WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/branches/{branchId:guid}/song-requests").RequireAuthorization(p => p.RequireRole("Owner", "Barista"));
        routes.MapGet("", async (Guid branchId, string? status, int? page, int? pageSize, HttpContext http, AppDbContext db, RequestScope scope, CancellationToken ct) =>
        {
            if (!await Allowed(branchId, http, db, scope, ct)) return Results.NotFound();
            var filter = status ?? "Pending";
            var requestedPage = page ?? 1; var size = pageSize ?? 20;
            if (filter is not ("All" or "Pending" or "Accepted" or "Rejected") || requestedPage is < 1 or > 10000 || size is < 1 or > 50)
                return Results.BadRequest(new { message = "Geçerli bir durum, sayfa ve 1–50 arası sayfa boyutu seçin." });
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
            var query = from r in db.SongRequests.AsNoTracking()
                                  join o in db.Orders on r.OrderId equals (Guid?)o.Id
                                  where r.BranchId == branchId && r.SongId == null && r.RequestedText != null && o.PaymentStatus == "Paid" && o.Status != "Cancelled"
                                  select new { r.Id, r.Status, r.Version, r.CreatedAt, text = r.RequestedText, o.PublicNumber };
            var pendingCount = await query.CountAsync(r => r.Status == "Pending", ct);
            var filtered = filter == "All" ? query : query.Where(r => r.Status == filter);
            var total = filter == "Pending" ? pendingCount : await filtered.CountAsync(ct);
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / size));
            var currentPage = Math.Min(requestedPage, totalPages);
            var items = await filtered.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                .Skip((currentPage - 1) * size).Take(size).ToListAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new { items, pendingCount, total, page = currentPage, pageSize = size, totalPages });
        });
        routes.MapPut("/{id:guid}", async (Guid branchId, Guid id, RequestSongDecision input, HttpContext http, AppDbContext db, RequestScope scope, CancellationToken ct) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await DatabaseLocks.Acquire(db, $"admin:{scope.TenantId}", ct);
            if (!await Allowed(branchId, http, db, scope, ct)) return Results.NotFound();
            var request = await db.SongRequests.SingleOrDefaultAsync(x => x.Id == id && x.BranchId == branchId && x.SongId == null && x.RequestedText != null && db.Orders.Any(o => o.Id == x.OrderId && o.PaymentStatus == "Paid" && o.Status != "Cancelled"), ct);
            if (request is null) return Results.NotFound();
            if (input.Status is not ("Accepted" or "Rejected")) return Results.BadRequest(new { message = "İsteği kabul edin veya reddedin." });
            if (request.Version != input.ExpectedVersion || request.Status != "Pending") return Results.Conflict(new { message = "İstek başka bir işlemde değişti. Listeyi yenileyin." });
            request.Status = input.Status; request.Version++;
            db.AuditLogs.Add(new() { TenantId = scope.TenantId!.Value, ActorId = scope.StaffId!.Value, EntityId = id, Action = "music.request." + input.Status.ToLowerInvariant(), OccurredAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.Ok(new { request.Id, request.Status, request.Version });
        });
    }
    private static Task<bool> Allowed(Guid branchId, HttpContext http, AppDbContext db, RequestScope scope, CancellationToken ct) =>
        db.Branches.AnyAsync(x => x.Id == branchId && (http.User.IsInRole("Owner") || db.StaffBranchAccess.Any(a => a.BranchId == x.Id && a.StaffId == scope.StaffId)), ct);
}
