using System.Globalization;
using System.Text;

namespace Kahve.Api.Modules.Music;

public static class MusicEndpoints
{
    // Retained for existing catalog data/import compatibility; no public catalog routes.
    public static string Normalize(string text) => string.Concat(text.Trim().ToLowerInvariant().Replace('ı', 'i').Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC);
    public static void MapMusicEndpoints(this WebApplication app) => OrderSongRequests.Map(app);
}
