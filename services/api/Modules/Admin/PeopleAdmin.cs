using System.Net.Mail;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Staff;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Admin;
public sealed record StaffInput(string Email, string Role, List<Guid>? BranchIds);
public sealed record StaffUpdate(string Role, bool Active, List<Guid>? BranchIds, int ExpectedVersion);
public sealed record RedeemInput(string Password);
public static partial class AdminEndpoints
{
    private static void MapPeople(RouteGroupBuilder group)
    {
        group.MapGet("/staff", async (AppDbContext db, CancellationToken ct) =>
        {
            var staff = await db.StaffUsers.AsNoTracking().OrderBy(x => x.Email).ToListAsync(ct);
            var access = await db.StaffBranchAccess.AsNoTracking().ToListAsync(ct);
            return Results.Ok(staff.Select(x => new { x.Id, x.Email, x.Role, x.Active, x.Version, invited = x.PasswordHash == "", branchIds = access.Where(a => a.StaffId == x.Id).Select(a => a.BranchId) }));
        });
        group.MapPost("/staff", async (StaffInput input, AppDbContext db, RequestScope scope, CancellationToken ct) =>
        {
            if (!Text(input.Email, 254) || !MailAddress.TryCreate(input.Email, out var address) || address.Address != input.Email || input.Role is not ("Owner" or "Barista") || !await ValidBranches(input.BranchIds, db, ct)) return Invalid("E-posta, rol ve şube yetkilerini kontrol edin.");
            var user = new StaffUser { Id = Guid.NewGuid(), TenantId = scope.TenantId!.Value, Email = input.Email.ToLowerInvariant(), Role = input.Role, Active = false };
            db.StaffUsers.Add(user);
            foreach (var branch in input.BranchIds!) db.StaffBranchAccess.Add(new() { TenantId = user.TenantId, StaffId = user.Id, BranchId = branch });
            var token = CapabilityTokens.New();
            db.StaffInvitations.Add(new() { Id = Guid.NewGuid(), TenantId = user.TenantId, StaffId = user.Id, TokenHash = CapabilityTokens.Hash(token), ExpiresAt = DateTimeOffset.UtcNow.AddHours(24) });
            Audit(db, scope, user.Id, "staff.invite"); await db.SaveChangesAsync(ct);
            return Results.Ok(new { user.Id, invitationUrl = $"/invite/{token}" });
        });
        group.MapPost("/staff/{id:guid}/invitation", async (Guid id, AppDbContext db, RequestScope scope, CancellationToken ct) =>
        {
            var user = await db.StaffUsers.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (user is null) return Results.NotFound();
            if (user.PasswordHash != "" || user.Active) return Invalid("Yalnızca henüz kabul edilmemiş davet yenilenebilir.");
            foreach (var invite in await db.StaffInvitations.Where(x => x.StaffId == id && x.RedeemedAt == null).ToListAsync(ct)) invite.ExpiresAt = DateTimeOffset.UtcNow;
            var token = CapabilityTokens.New();
            db.StaffInvitations.Add(new() { Id = Guid.NewGuid(), TenantId = user.TenantId, StaffId = id, TokenHash = CapabilityTokens.Hash(token), ExpiresAt = DateTimeOffset.UtcNow.AddHours(24) });
            Audit(db, scope, id, "staff.reinvite"); await db.SaveChangesAsync(ct); return Results.Ok(new { invitationUrl = $"/invite/{token}" });
        });
        group.MapPut("/staff/{id:guid}", async (Guid id, StaffUpdate input, AppDbContext db, RequestScope scope, CancellationToken ct) =>
        {
            var user = await db.StaffUsers.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (user is null) return Results.NotFound();
            if (user.Version != input.ExpectedVersion) return Conflict();
            if (id == scope.StaffId && (input.Role != "Owner" || !input.Active)) return Invalid("Kendi yönetici erişiminizi kapatamazsınız.");
            if (input.Role is not ("Owner" or "Barista") || !await ValidBranches(input.BranchIds, db, ct) || (user.PasswordHash == "" && input.Active)) return Invalid("Rol, şube veya davet durumu geçersiz.");
            if (user.Role == "Owner" && user.Active && (!input.Active || input.Role != "Owner") && await db.StaffUsers.CountAsync(x => x.Active && x.Role == "Owner", ct) <= 1) return Invalid("Son aktif işletme sahibi kapatılamaz.");
            user.Role = input.Role; user.Active = input.Active; user.Version++;
            var old = await db.StaffBranchAccess.Where(x => x.StaffId == id).ToListAsync(ct);
            db.StaffBranchAccess.RemoveRange(old.Where(x => !input.BranchIds!.Contains(x.BranchId)));
            foreach (var branch in input.BranchIds!.Where(b => !old.Any(x => x.BranchId == b))) db.StaffBranchAccess.Add(new() { TenantId = user.TenantId, StaffId = id, BranchId = branch });
            Audit(db, scope, id, "staff.update"); await NotifyBranches(db, ct); await db.SaveChangesAsync(ct); return Results.Ok(new { user.Id, user.Version });
        });
    }
    private static async Task<bool> ValidBranches(List<Guid>? ids, AppDbContext db, CancellationToken ct) => ids is { Count: > 0 and <= 100 } && ids.Distinct().Count() == ids.Count && await db.Branches.CountAsync(x => ids.Contains(x.Id), ct) == ids.Count;
    private static void MapInvitation(WebApplication app)
    {
        app.MapGet("/api/invitations/{token}", async (string token, AppDbContext db, CancellationToken ct) =>
        {
            if (!CapabilityTokens.Valid(token)) return Results.NotFound();
            var hash = CapabilityTokens.Hash(token);
            var invite = await db.StaffInvitations.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
            if (invite is null) return Results.NotFound();
            if (invite.RedeemedAt != null || invite.ExpiresAt <= DateTimeOffset.UtcNow) return Results.Json(new { message = "Davetin süresi doldu veya kullanıldı." }, statusCode: 410);
            var user = await db.StaffUsers.IgnoreQueryFilters().SingleAsync(x => x.Id == invite.StaffId && x.TenantId == invite.TenantId, ct);
            return Results.Ok(new { user.Email, user.Role, invite.ExpiresAt });
        }).RequireRateLimiting("tracking");
        app.MapPost("/api/invitations/{token}/redeem", async (string token, RedeemInput input, AppDbContext db, CancellationToken ct) =>
        {
            if (!CapabilityTokens.Valid(token)) return Results.NotFound();
            if (input.Password is null || input.Password.Length is < 12 or > 128) return Invalid("Parola 12–128 karakter olmalı.");
            var hash = CapabilityTokens.Hash(token);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await DatabaseLocks.Acquire(db, $"invitation:{hash}", ct);
            var invite = await db.StaffInvitations.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
            if (invite is null) return Results.NotFound();
            // Tenant lock serializes activation against owner-managed account changes.
            await DatabaseLocks.Acquire(db, $"admin:{invite.TenantId}", ct);
            await db.Entry(invite).ReloadAsync(ct);
            if (invite.RedeemedAt != null || invite.ExpiresAt <= DateTimeOffset.UtcNow) return Results.Json(new { message = "Davetin süresi doldu veya kullanıldı." }, statusCode: 410);
            var user = await db.StaffUsers.IgnoreQueryFilters().SingleAsync(x => x.Id == invite.StaffId && x.TenantId == invite.TenantId, ct);
            if (user.PasswordHash != "") return Invalid("Bu hesap zaten etkinleştirildi.");
            user.PasswordHash = new PasswordHasher<StaffUser>().HashPassword(user, input.Password); user.Active = true; user.Version++; invite.RedeemedAt = DateTimeOffset.UtcNow;
            db.AuditLogs.Add(new() { TenantId = invite.TenantId, ActorId = user.Id, EntityId = user.Id, Action = "staff.activate", OccurredAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { activated = true });
        }).RequireRateLimiting("invitation");
    }
}
