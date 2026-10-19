using System.Security.Claims;
using Kahve.Api.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Kahve.Api.Modules.Tenancy;

namespace Kahve.Api.Modules.Staff;

public static class AuthEndpoints
{
    private static readonly string DummyHash = new PasswordHasher<StaffUser>().HashPassword(new StaffUser(), Guid.NewGuid().ToString());
    public static async Task ValidatePrincipal(CookieValidatePrincipalContext c)
    {
        var db = c.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var principal = c.Principal!;
        var tenant = Guid.Parse(principal.FindFirstValue("tenant_id")!);
        var staff = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = principal.FindFirstValue(ClaimTypes.Role);
        if (!await db.StaffUsers.IgnoreQueryFilters().AnyAsync(x => x.Id == staff && x.TenantId == tenant && x.Active && x.Role == role)) c.RejectPrincipal();
    }

    public static void MapStaffEndpoints(this WebApplication app)
    {
        app.MapGet("/api/session/csrf", (HttpContext c, IAntiforgery antiforgery) => Results.Ok(new { token = antiforgery.GetAndStoreTokens(c).RequestToken }));
        app.MapPost("/api/session/login", Login).RequireRateLimiting("login");
        app.MapPost("/api/session/logout", async (HttpContext c) => { await c.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
        app.MapGet("/api/session/me", (HttpContext c) => Results.Ok(new { email = c.User.Identity!.Name, role = c.User.FindFirstValue(ClaimTypes.Role) })).RequireAuthorization();
        app.MapGet("/api/staff/branches", async (AppDbContext db, RequestScope scope) =>
        {
            var branches = await db.Branches.AsNoTracking().Where(b => db.StaffBranchAccess.Any(a => a.StaffId == scope.StaffId && a.BranchId == b.Id)).ToListAsync();
            return Results.Ok(branches.Select(b => new { b.Id, b.Name, b.Slug, b.IsAcceptingOrders, b.Version, b.PreparationCapacity, isOpenNow = b.IsAcceptingOrders && BranchHours.IsOpen(b, DateTimeOffset.UtcNow) }));
        }).RequireAuthorization();
        app.MapPatch("/api/staff/branches/{id:guid}/accepting-orders", SetAcceptance).RequireAuthorization();
        app.MapGet("/api/admin/products", async (AppDbContext db) => Results.Ok(await db.Products.AsNoTracking().OrderBy(x => x.SortOrder)
            .Select(x => new { x.Id, x.Name, category = x.Category.Name, x.BasePrice, x.Active }).ToListAsync())).RequireAuthorization(p => p.RequireRole("Owner"));
    }

    private static async Task<IResult> Login(LoginRequest input, AppDbContext db, HttpContext c)
    {
        if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 254 || string.IsNullOrEmpty(input.Password) || input.Password.Length > 256)
            return Results.BadRequest(new { message = "E-posta ve parola gerekli." });
        var email = input.Email.Trim().ToLowerInvariant();
        // Global lookup only for credentials; tenant comes from the verified account.
        var user = await db.StaffUsers.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Email == email && x.Active);
        var hasher = new PasswordHasher<StaffUser>();
        var candidate = user ?? new StaffUser { PasswordHash = DummyHash };
        var verified = hasher.VerifyHashedPassword(candidate, candidate.PasswordHash, input.Password);
        if (user is null || verified == PasswordVerificationResult.Failed) return Results.Json(new { message = "E-posta veya parola yanlış." }, statusCode: 401);
        if (verified == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, input.Password);
            await db.SaveChangesAsync();
        }
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim("tenant_id", user.TenantId.ToString()), new Claim(ClaimTypes.Name, user.Email), new Claim(ClaimTypes.Role, user.Role) };
        await c.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return Results.Ok(new { user.Email, user.Role });
    }

    private static async Task<IResult> SetAcceptance(Guid id, AcceptanceRequest input, AppDbContext db, RequestScope scope)
    {
        var branch = await db.Branches.SingleOrDefaultAsync(b => b.Id == id && db.StaffBranchAccess.Any(a => a.StaffId == scope.StaffId && a.BranchId == b.Id));
        if (branch is null) return Results.NotFound();
        if (branch.Version != input.ExpectedVersion) return Results.Conflict(new { message = "Şube başka bir personel tarafından güncellendi. Yenileyin." });
        branch.IsAcceptingOrders = input.IsAcceptingOrders;
        branch.Version++;
        db.RealtimeEvents.Add(new Kahve.Api.Modules.Realtime.RealtimeEvent { Id = Guid.NewGuid(), TenantId = branch.TenantId, BranchId = branch.Id, Version = branch.Version, CreatedAt = DateTimeOffset.UtcNow });
        db.AuditLogs.Add(new AuditLog { TenantId = branch.TenantId, ActorId = scope.StaffId!.Value, EntityId = id, Action = input.IsAcceptingOrders ? "branch.open" : "branch.pause", OccurredAt = DateTimeOffset.UtcNow });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "Şube değişti. Yenileyin." }); }
        return Results.Ok(new { branch.Id, branch.IsAcceptingOrders, branch.Version });
    }
}
public sealed record LoginRequest(string Email, string Password);
public sealed record AcceptanceRequest(bool IsAcceptingOrders, int ExpectedVersion);
