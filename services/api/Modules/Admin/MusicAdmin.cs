using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Music;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Admin;
public sealed record MusicSettingInput(bool Enabled, int ExpectedVersion);
public sealed record SongInput(string Title, string Artist);
public sealed record SongAvailability(bool Active);
public sealed record SongDecision(string Status, int ExpectedVersion);
public static partial class AdminEndpoints
{
    private static void MapMusic(RouteGroupBuilder group)
    {
        MapMusicBrainzImport(group);
        group.MapGet("/music/{branchId:guid}", async (Guid branchId, AppDbContext db, CancellationToken ct) =>
        {
            if (!await db.Branches.AnyAsync(x => x.Id == branchId, ct)) return Results.NotFound();
            var setting = await db.BranchMusic.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branchId, ct);
            var pool = await db.BranchSongs.AsNoTracking().Where(x => x.BranchId == branchId).ToListAsync(ct);
            var songs = await db.Songs.AsNoTracking().OrderBy(x => x.Artist).ThenBy(x => x.Title).ToListAsync(ct);
            var requests = await (from r in db.SongRequests.AsNoTracking()
                                  join song in db.Songs on r.SongId equals (Guid?)song.Id into requestSongs
                                  from s in requestSongs.DefaultIfEmpty()
                                  join o in db.Orders on r.OrderId equals (Guid?)o.Id into orders
                                  from o in orders.DefaultIfEmpty()
                                  where r.BranchId == branchId
                                  orderby r.CreatedAt descending
                                  select new { r.Id, r.Status, r.Version, r.CreatedAt, title = s == null ? r.RequestedText! : s.Title, artist = s == null ? "" : s.Artist, isCustom = r.SongId == null, publicNumber = o == null ? (long?)null : o.PublicNumber }).Take(100).ToListAsync(ct);
            return Results.Ok(new { enabled = setting?.Enabled ?? false, version = setting?.Version ?? 0, songs = songs.Select(s => new { s.Id, s.Title, s.Artist, s.MusicBrainzRecordingId, active = pool.Any(p => p.SongId == s.Id && p.Active) }), requests });
        });
        group.MapPut("/music/{branchId:guid}/settings", async (Guid branchId, MusicSettingInput input, AppDbContext db, RequestScope scope, CancellationToken ct) =>
        {
            if (!await db.Branches.AnyAsync(x => x.Id == branchId, ct)) return Results.NotFound();
            var setting = await db.BranchMusic.SingleOrDefaultAsync(x => x.BranchId == branchId, ct);
            if ((setting?.Version ?? 0) != input.ExpectedVersion) return Conflict();
            if (setting is null) { setting = new() { BranchId = branchId, TenantId = scope.TenantId!.Value }; db.BranchMusic.Add(setting); }
            else setting.Version++;
            setting.Enabled = input.Enabled;
            Audit(db, scope, branchId, "music.settings"); await db.SaveChangesAsync(ct);
            return Results.Ok(new { setting.Version });
        });
        group.MapPost("/music/{branchId:guid}/songs", async (Guid branchId, SongInput input, AppDbContext db, RequestScope scope, CancellationToken ct) =>
        {
            if (!await db.Branches.AnyAsync(x => x.Id == branchId, ct)) return Results.NotFound();
            if (!Text(input.Title, 160) || !Text(input.Artist, 160)) return Invalid("Şarkı adı ve sanatçı 1–160 karakter olmalı.");
            var key = MusicEndpoints.Normalize(input.Title) + " | " + MusicEndpoints.Normalize(input.Artist);
            var song = await db.Songs.SingleOrDefaultAsync(x => x.SearchKey == key, ct);
            if (song is null) { song = new() { Id = Guid.NewGuid(), TenantId = scope.TenantId!.Value, Title = input.Title.Trim(), Artist = input.Artist.Trim(), SearchKey = key }; db.Songs.Add(song); }
            var pool = await db.BranchSongs.SingleOrDefaultAsync(x => x.BranchId == branchId && x.SongId == song.Id, ct);
            if (pool is null) db.BranchSongs.Add(new() { TenantId = song.TenantId, BranchId = branchId, SongId = song.Id }); else pool.Active = true;
            Audit(db, scope, song.Id, "music.song.add"); await db.SaveChangesAsync(ct); return Results.Ok(new { song.Id });
        });
        group.MapPut("/music/{branchId:guid}/songs/{songId:guid}", async (Guid branchId, Guid songId, SongAvailability input, AppDbContext db, RequestScope scope, CancellationToken ct) =>
        {
            if (!await db.Branches.AnyAsync(x => x.Id == branchId, ct) || !await db.Songs.AnyAsync(x => x.Id == songId, ct)) return Results.NotFound();
            var pool = await db.BranchSongs.SingleOrDefaultAsync(x => x.BranchId == branchId && x.SongId == songId, ct);
            if (pool is null) { pool = new() { TenantId = scope.TenantId!.Value, BranchId = branchId, SongId = songId }; db.BranchSongs.Add(pool); }
            pool.Active = input.Active; Audit(db, scope, songId, "music.song.availability"); await db.SaveChangesAsync(ct); return Results.NoContent();
        });
        group.MapPut("/music/{branchId:guid}/requests/{id:guid}", async (Guid branchId, Guid id, SongDecision input, AppDbContext db, RequestScope scope, CancellationToken ct) =>
        {
            var request = await db.SongRequests.SingleOrDefaultAsync(x => x.Id == id && x.BranchId == branchId, ct);
            if (request is null) return Results.NotFound();
            if (request.Version != input.ExpectedVersion || request.Status != "Pending") return Conflict();
            if (input.Status is not ("Accepted" or "Rejected")) return Invalid("Öneriyi kabul edin veya reddedin.");
            request.Status = input.Status; request.Version++;
            Audit(db, scope, id, "music.request." + input.Status.ToLowerInvariant()); await db.SaveChangesAsync(ct); return Results.Ok(new { request.Version });
        });
    }
}
