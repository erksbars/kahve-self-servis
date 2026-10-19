using System.Globalization;
using System.Text.Json;

namespace Kahve.Api.Modules.Tenancy;
public sealed record OpeningWindow(int Day, string Opens, string Closes);
public static class BranchHours
{
    public static bool IsOpen(Branch branch, DateTimeOffset utc)
    {
        var windows = JsonSerializer.Deserialize<List<OpeningWindow>>(branch.HoursJson) ?? [];
        if (windows.Count == 0) return true;
        var local = TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById(branch.TimeZone));
        var time = TimeOnly.FromDateTime(local.DateTime);
        return windows.Any(x => x.Day == (int)local.DayOfWeek && time >= TimeOnly.ParseExact(x.Opens, "HH:mm", CultureInfo.InvariantCulture) && time < TimeOnly.ParseExact(x.Closes, "HH:mm", CultureInfo.InvariantCulture));
    }
    public static bool Valid(List<OpeningWindow>? windows) => windows is not null && windows.Count <= 7 && windows.All(x => x is not null) && windows.Select(x => x.Day).Distinct().Count() == windows.Count && windows.All(x => x.Day is >= 0 and <= 6 && TimeOnly.TryParseExact(x.Opens, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var opens) && TimeOnly.TryParseExact(x.Closes, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var closes) && opens < closes);
}
