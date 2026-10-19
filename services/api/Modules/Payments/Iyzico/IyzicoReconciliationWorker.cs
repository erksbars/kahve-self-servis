using Kahve.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Payments.Iyzico;
public sealed class IyzicoReconciliationWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<IyzicoReconciliationWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (!config.GetValue("Iyzico:RunWorker", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stop))
        {
            try
            {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var since = DateTimeOffset.UtcNow.AddDays(-7); var retry = DateTimeOffset.UtcNow.AddSeconds(-25);
                var payments = await (from p in db.Payments.IgnoreQueryFilters().AsNoTracking() join o in db.Orders.IgnoreQueryFilters().AsNoTracking() on p.OrderId equals o.Id where p.Provider == "iyzico" && p.Status == "Pending" && p.ProtectedProviderToken != "" && o.CreatedAt >= since && (p.LastVerifiedAt == null || p.LastVerifiedAt < retry) orderby p.LastVerifiedAt select new { p.Id, p.ProtectedProviderToken }).Take(10).ToListAsync(stop);
                foreach (var payment in payments) { var tokens = scope.ServiceProvider.GetRequiredService<CapabilityTokens>(); await scope.ServiceProvider.GetRequiredService<IyzicoFlow>().Verify(payment.Id, tokens.Unprotect(payment.ProtectedProviderToken), stop); }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception) { log.LogWarning("iyzico reconciliation attempt failed; pending payments are retained."); }
        }
    }
}
