using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Realtime;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kahve.Api.Modules.Admin;
public static partial class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization(p => p.RequireRole("Owner"));
        group.AddEndpointFilter(async (context, next) =>
        {
            if (HttpMethods.IsGet(context.HttpContext.Request.Method)) return await next(context);
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var scope = context.HttpContext.RequestServices.GetRequiredService<RequestScope>();
            var ct = context.HttpContext.RequestAborted;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await DatabaseLocks.Acquire(db, $"admin:{scope.TenantId}", ct);
            try
            {
                var result = await next(context);
                if (result is not IStatusCodeHttpResult { StatusCode: >= 400 }) await tx.CommitAsync(ct);
                return result;
            }
            catch (DbUpdateConcurrencyException) { return Conflict(); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" }) { return Results.Conflict(new { message = "Bu adres veya kayıt zaten kullanılıyor." }); }
        });
        MapCatalog(group); MapProductImages(group); MapBranches(group); MapPeople(group); MapReports(group); MapMusic(group);
        MapInvitation(app);
    }
    private static IResult Invalid(string message) => Results.BadRequest(new { message });
    private static IResult Conflict() => Results.Conflict(new { message = "Kayıt başka bir işlemde değişti. Yenileyip tekrar deneyin." });
    private static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max;
    private static bool Price(decimal value) => value is >= 0 and <= 1000000 && decimal.Round(value, 2) == value;
    private static void Audit(AppDbContext db, RequestScope scope, Guid id, string action) => db.AuditLogs.Add(new() { TenantId = scope.TenantId!.Value, ActorId = scope.StaffId!.Value, EntityId = id, Action = action, OccurredAt = DateTimeOffset.UtcNow });
    private static async Task NotifyBranches(AppDbContext db, CancellationToken ct)
    {
        foreach (var branch in await db.Branches.ToListAsync(ct)) db.RealtimeEvents.Add(new RealtimeEvent { Id = Guid.NewGuid(), TenantId = branch.TenantId, BranchId = branch.Id, Version = branch.Version, CreatedAt = DateTimeOffset.UtcNow });
    }
}
