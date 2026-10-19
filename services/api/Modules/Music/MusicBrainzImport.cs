using System.Text.Json;

namespace Kahve.Api.Modules.Music;

public sealed record MusicBrainzImportInput(JsonElement Data, bool Preview = true);
public sealed record MusicBrainzRecording(Guid Id, string Title, string Artist)
{
    public string Key => MusicEndpoints.Normalize(Title) + " | " + MusicEndpoints.Normalize(Artist);
}

// Only CC0 core fields are retained. Tags, ratings, artwork and URLs are deliberately ignored.
public static class MusicBrainzImport
{
    public static IReadOnlyList<MusicBrainzRecording> Parse(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("recordings", out var recordings)) data = recordings;
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() is < 1 or > 500)
            throw new FormatException("JSON dosyası 1–500 kayıt içeren recordings listesi veya kayıt dizisi olmalı.");
        var result = new List<MusicBrainzRecording>();
        var ids = new Dictionary<Guid, MusicBrainzRecording>();
        var keys = new HashSet<string>();
        var row = 0;
        foreach (var entry in data.EnumerateArray())
        {
            row++;
            if (entry.ValueKind != JsonValueKind.Object || !Guid.TryParse(Value(entry, "id"), out var id) || id == Guid.Empty)
                throw new FormatException($"{row}. kayıtta geçerli bir MusicBrainz recording ID bulunamadı.");
            var title = Value(entry, "title").Trim();
            var artist = "";
            if (entry.TryGetProperty("artist-credit", out var credits) && credits.ValueKind == JsonValueKind.Array)
            {
                foreach (var credit in credits.EnumerateArray())
                {
                    if (credit.ValueKind == JsonValueKind.String) artist += credit.GetString();
                    else if (credit.ValueKind == JsonValueKind.Object)
                    {
                        var name = Value(credit, "name");
                        if (name.Length == 0 && credit.TryGetProperty("artist", out var a) && a.ValueKind == JsonValueKind.Object) name = Value(a, "name");
                        artist += name + Value(credit, "joinphrase");
                    }
                }
            }
            artist = artist.Trim();
            if (!Valid(title) || !Valid(artist)) throw new FormatException($"{row}. kayıtta şarkı adı ve sanatçı 1–160 karakter olmalı; kontrol karakterleri kullanılamaz.");
            var item = new MusicBrainzRecording(id, title, artist);
            if (ids.TryGetValue(id, out var prior))
            {
                if (prior.Key != item.Key) throw new FormatException($"{row}. kayıtta aynı MusicBrainz ID farklı şarkılar için kullanılmış.");
                continue;
            }
            ids.Add(id, item);
            // Different recordings/masters with the same title and artist share a menu entry.
            if (keys.Add(item.Key)) result.Add(item);
        }
        return result;
    }
    private static string Value(JsonElement item, string key) => item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
    private static bool Valid(string value) => value.Length is >= 1 and <= 160 && !value.Any(char.IsControl);
}
