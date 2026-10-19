using Kahve.Api.Modules.Music;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Infrastructure;
public static class DemoMusicCatalog
{
    // A demo-only, locally curated metadata list. No audio, lyrics or external service calls.
    private static readonly (string Title, string Artist)[] Tracks = [
        ("Gülümse", "Sezen Aksu"), ("Firuze", "Sezen Aksu"), ("Hadi Bakalım", "Sezen Aksu"),
        ("Gülpembe", "Barış Manço"), ("Dönence", "Barış Manço"), ("Sarı Çizmeli Mehmet Ağa", "Barış Manço"),
        ("Ele Güne Karşı", "MFÖ"), ("Sarı Laleler", "MFÖ"), ("Bu Sabah Yağmur Var İstanbul'da", "MFÖ"),
        ("Her Şeyi Yak", "Duman"), ("Senden Daha Güzel", "Duman"), ("Belki Alışman Lazım", "Duman"),
        ("Paramparça", "Teoman"), ("Renkli Rüyalar Oteli", "Teoman"), ("Papatya", "Teoman"),
        ("Dünyadan Uzak", "Sakiler"), ("Sadece", "Kalben"), ("Yara", "Kalben"),
        ("Aşkın Olayım", "Simge"), ("Yalnız Çiçek", "Aleyna Tilki"), ("Antidepresan", "Mert Demir & Mabel Matiz"),
        ("Öyle Kolaysa", "Mabel Matiz"), ("Sarmaşık", "Mabel Matiz"), ("Fırtınadayım", "Mabel Matiz"),
        ("Don't Know Why", "Norah Jones"), ("Sunrise", "Norah Jones"), ("Come Away With Me", "Norah Jones"),
        ("Feeling Good", "Nina Simone"), ("My Baby Just Cares for Me", "Nina Simone"), ("Dream a Little Dream of Me", "Ella Fitzgerald & Louis Armstrong"),
        ("What a Wonderful World", "Louis Armstrong"), ("La Vie en rose", "Édith Piaf"), ("Fly Me to the Moon", "Frank Sinatra"),
        ("Lovely Day", "Bill Withers"), ("Just the Two of Us", "Grover Washington, Jr. & Bill Withers"), ("Ain't No Sunshine", "Bill Withers"),
        ("Banana Pancakes", "Jack Johnson"), ("Better Together", "Jack Johnson"), ("Put Your Records On", "Corinne Bailey Rae"),
        ("Sunday Morning", "Maroon 5"), ("Yellow", "Coldplay"), ("Sparks", "Coldplay"),
        ("Dreams", "Fleetwood Mac"), ("Here Comes the Sun", "The Beatles"), ("Let It Be", "The Beatles"),
        ("Take Five", "The Dave Brubeck Quartet"), ("So What", "Miles Davis"), ("Blue in Green", "Miles Davis")
    ];
    public static async Task Run(AppDbContext db, int version = 4)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await DatabaseLocks.Acquire(db, "demo-music-v1", default);
        if (await db.DemoSeedMarkers.AnyAsync(x => x.Id == version)) return;
        var tenants = await db.Tenants.IgnoreQueryFilters().Where(x => x.Name == "Demo Kahve A" || x.Name == "Demo Kahve B").Select(x => x.Id).ToListAsync();
        foreach (var tenant in tenants)
        {
            var branches = await db.Branches.IgnoreQueryFilters().Where(x => x.TenantId == tenant).ToListAsync();
            foreach (var branch in branches.Where(_ => version == 4))
                if (!await db.BranchMusic.IgnoreQueryFilters().AnyAsync(x => x.BranchId == branch.Id))
                    db.BranchMusic.Add(new() { BranchId = branch.Id, TenantId = tenant, Enabled = true });
            foreach (var (title, artist) in version == 5 ? new[] { ("Still Loving You", "Scorpions") } : Tracks)
            {
                var key = MusicEndpoints.Normalize(title) + " | " + MusicEndpoints.Normalize(artist);
                var song = await db.Songs.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.TenantId == tenant && x.SearchKey == key);
                if (song is null) { song = new() { Id = Guid.NewGuid(), TenantId = tenant, Title = title, Artist = artist, SearchKey = key }; db.Songs.Add(song); }
                foreach (var branch in branches)
                    if (!await db.BranchSongs.IgnoreQueryFilters().AnyAsync(x => x.BranchId == branch.Id && x.SongId == song.Id))
                        db.BranchSongs.Add(new() { TenantId = tenant, BranchId = branch.Id, SongId = song.Id });
            }
        }
        db.DemoSeedMarkers.Add(new() { Id = version });
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
}
