using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Music;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Admin;

public static partial class AdminEndpoints
{
    private static void MapMusicBrainzImport(RouteGroupBuilder group)
    {
        group.MapPost("/music/{branchId:guid}/musicbrainz/import", async (Guid branchId, MusicBrainzImportInput input, AppDbContext db, RequestScope scope, CancellationToken ct) =>
        {
            if (!await db.Branches.AnyAsync(x => x.Id == branchId, ct)) return Results.NotFound();
            IReadOnlyList<MusicBrainzRecording> recordings;
            try { recordings = MusicBrainzImport.Parse(input.Data); }
            catch (FormatException e) { return Invalid(e.Message); }
            var ids = recordings.Select(x => x.Id).ToArray();
            var keys = recordings.Select(x => x.Key).ToArray();
            var existing = await db.Songs.Where(x => keys.Contains(x.SearchKey) || (x.MusicBrainzRecordingId != null && ids.Contains(x.MusicBrainzRecordingId.Value))).ToListAsync(ct);
            var pool = await db.BranchSongs.Where(x => x.BranchId == branchId).ToListAsync(ct);
            var added = 0; var linked = 0;
            var rows = new List<object>();
            foreach (var recording in recordings)
            {
                var song = existing.FirstOrDefault(x => x.MusicBrainzRecordingId == recording.Id) ?? existing.FirstOrDefault(x => x.SearchKey == recording.Key);
                var isNew = song is null;
                var inBranch = song is null ? null : pool.FirstOrDefault(x => x.SongId == song.Id);
                rows.Add(new { recording.Id, title = song?.Title ?? recording.Title, artist = song?.Artist ?? recording.Artist, isNew, inBranch = inBranch is not null, active = inBranch?.Active ?? false });
                if (isNew) added++;
                if (inBranch is null) linked++;
                if (input.Preview) continue;
                if (song is null)
                {
                    song = new Song { Id = Guid.NewGuid(), TenantId = scope.TenantId!.Value, Title = recording.Title, Artist = recording.Artist, SearchKey = recording.Key, MusicBrainzRecordingId = recording.Id };
                    db.Songs.Add(song); existing.Add(song);
                }
                // Preserve existing metadata and disabled choices on re-import.
                else if (song.MusicBrainzRecordingId is null) song.MusicBrainzRecordingId = recording.Id;
                if (inBranch is null)
                {
                    var link = new BranchSong { TenantId = scope.TenantId!.Value, BranchId = branchId, SongId = song.Id, Active = false };
                    db.BranchSongs.Add(link); pool.Add(link);
                }
            }
            if (!input.Preview)
            {
                Audit(db, scope, branchId, "music.musicbrainz.import");
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(new { preview = input.Preview, total = recordings.Count, added, linked, unchanged = recordings.Count - added, rows });
        }).WithMetadata(new RequestSizeLimitAttribute(1024 * 1024));
    }
}
