using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Payments;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/branches/{slug}/menu", async (string slug, AppDbContext db, IConfiguration config, CancellationToken ct) =>
        {
            var menu = await CatalogReader.Read(db, slug, config.GetValue<bool>("Demo:Enabled"), ct);
            if (menu is null) return Results.NotFound(new { message = "Şube bulunamadı." });
            var selected = PaymentProviders.Selection(config); var ready = CheckoutEndpoints.Enabled(config);
            if (selected == "iyzico")
            {
                var branch = await db.Branches.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Slug == slug, ct);
                try { ready = new Kahve.Api.Modules.Payments.Iyzico.IyzicoMerchantAccounts(config).Find(branch.TenantId) is not null; }
                catch (Exception e) when (e is ArgumentException or InvalidOperationException) { ready = false; }
            }
            return Results.Ok(menu with { CheckoutEnabled = ready, CheckoutProvider = selected });
        });
    }
}
