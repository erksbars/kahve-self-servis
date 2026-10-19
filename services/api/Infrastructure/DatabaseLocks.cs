using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Infrastructure;
public static class DatabaseLocks
{
    public static Task Acquire(AppDbContext db, string key, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
}
