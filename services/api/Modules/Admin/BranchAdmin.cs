using System.Text.Json;
using System.Text.RegularExpressions;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Catalog;
using Kahve.Api.Modules.Tenancy;
using Kahve.Api.Modules.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Admin;
public sealed record BranchInput(string Name, string Slug, string Address, string TimeZone, bool IsAcceptingOrders, int PreparationCapacity, List<OpeningWindow>? Hours, int ExpectedVersion = 0);
public static partial class AdminEndpoints
{
    private static void MapBranches(RouteGroupBuilder group)
    {
        group.MapGet("/branches", async (AppDbContext db, CancellationToken ct) => Results.Ok((await db.Branches.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)).Select(x => new { x.Id, x.Name, x.Slug, x.Address, x.TimeZone, x.IsAcceptingOrders, x.PreparationCapacity, x.Version, isOpenNow = x.IsAcceptingOrders && BranchHours.IsOpen(x, DateTimeOffset.UtcNow), hours = JsonSerializer.Deserialize<List<OpeningWindow>>(x.HoursJson) })));
        group.MapPost("/branches", (BranchInput input, AppDbContext db, RequestScope scope, CancellationToken ct) => SaveBranch(null, input, db, scope, ct));
        group.MapPut("/branches/{id:guid}", (Guid id, BranchInput input, AppDbContext db, RequestScope scope, CancellationToken ct) => SaveBranch(id, input, db, scope, ct));
    }
    private static async Task<IResult> SaveBranch(Guid? id, BranchInput input, AppDbContext db, RequestScope scope, CancellationToken ct)
    {
        var branch = id.HasValue ? await db.Branches.SingleOrDefaultAsync(x => x.Id == id, ct) : new Branch { Id = Guid.NewGuid(), TenantId = scope.TenantId!.Value };
        if (branch is null) return Results.NotFound();
        if (id.HasValue && branch.Version != input.ExpectedVersion) return Conflict();
        if (!Text(input.Name, 80) || !Text(input.Slug, 100) || !Regex.IsMatch(input.Slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$") || input.Address is null || input.Address.Length > 250 || input.PreparationCapacity is < 1 or > 1000 || !BranchHours.Valid(input.Hours)) return Invalid("Şube adı, adresi, bağlantı adresi, kapasite veya saatler geçersiz.");
        try { if (!Text(input.TimeZone, 100) || (input.TimeZone != "UTC" && !input.TimeZone.Contains('/'))) return Invalid("IANA saat dilimi girin."); _ = TimeZoneInfo.FindSystemTimeZoneById(input.TimeZone); }
        catch (TimeZoneNotFoundException) { return Invalid("Saat dilimi bulunamadı."); }
        catch (InvalidTimeZoneException) { return Invalid("Saat dilimi geçersiz."); }
        if (id.HasValue && branch.Slug != input.Slug) return Invalid("QR bağlantısını korumak için mevcut şube adresi değiştirilemez.");
        branch.Name = input.Name.Trim(); branch.Slug = input.Slug; branch.Address = input.Address.Trim(); branch.TimeZone = input.TimeZone; branch.IsAcceptingOrders = input.IsAcceptingOrders; branch.PreparationCapacity = input.PreparationCapacity; branch.HoursJson = JsonSerializer.Serialize(input.Hours);
        if (id.HasValue) branch.Version++; else
        {
            db.Branches.Add(branch);
            foreach (var product in await db.Products.ToListAsync(ct)) db.BranchProducts.Add(new BranchProduct { TenantId = branch.TenantId, BranchId = branch.Id, ProductId = product.Id });
            foreach (var owner in await db.StaffUsers.Where(x => x.Role == "Owner" && x.Active).ToListAsync(ct)) db.StaffBranchAccess.Add(new StaffBranchAccess { TenantId = branch.TenantId, BranchId = branch.Id, StaffId = owner.Id });
        }
        Audit(db, scope, branch.Id, id.HasValue ? "branch.update" : "branch.create"); await NotifyBranches(db, ct); await db.SaveChangesAsync(ct); return Results.Ok(new { branch.Id, branch.Version });
    }
}
