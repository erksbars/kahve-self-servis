using Kahve.Api.Modules.Catalog;
using Kahve.Api.Modules.Staff;
using Kahve.Api.Modules.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Infrastructure;

public static class DemoSeed
{
    public static async Task Run(AppDbContext db, string password)
    {
        if (password.Length < 12) throw new InvalidOperationException("Demo password must have at least 12 characters.");
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync())
        {
            await SeedOptions(db);
            await PremiumDemoCatalog.Run(db);
            await PremiumDemoCatalog.Run(db, 3);
            await DemoMusicCatalog.Run(db);
            await DemoMusicCatalog.Run(db, 5);
            return;
        }
        var hasher = new PasswordHasher<StaffUser>();
        foreach (var (name, slug, email) in new[] {
            ("Demo Kahve A", "demo-moda", "owner-a@demo.local"),
            ("Demo Kahve B", "demo-besiktas", "owner-b@demo.local") })
        {
            var tenant = new Tenant { Id = Guid.NewGuid(), Name = name };
            var branch = new Branch { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = slug == "demo-moda" ? "Moda" : "Beşiktaş", Slug = slug, Address = "Örnek şube · İstanbul" };
            var second = new Branch { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "İkinci şube", Slug = slug + "-2", Address = "Örnek ikinci şube · İstanbul" };
            var owner = new StaffUser { Id = Guid.NewGuid(), TenantId = tenant.Id, Email = email, Role = "Owner" };
            var barista = new StaffUser { Id = Guid.NewGuid(), TenantId = tenant.Id, Email = email.Replace("owner", "barista"), Role = "Barista" };
            owner.PasswordHash = hasher.HashPassword(owner, password);
            barista.PasswordHash = hasher.HashPassword(barista, password);
            db.AddRange(tenant, branch, second, owner, barista);
            db.StaffBranchAccess.AddRange(new StaffBranchAccess { TenantId = tenant.Id, StaffId = owner.Id, BranchId = branch.Id }, new StaffBranchAccess { TenantId = tenant.Id, StaffId = owner.Id, BranchId = second.Id }, new StaffBranchAccess { TenantId = tenant.Id, StaffId = barista.Id, BranchId = branch.Id });
            var categories = new Dictionary<string, Category>();
            foreach (var (category, title, description, price) in new[] {
                ("Espresso bazlı", "Flat White", "Çift espresso, kadifemsi süt. Dengeli ve yoğun.", 140m),
                ("Espresso bazlı", "Latte", "Espresso ve bol sıcak süt. Yumuşak bir başlangıç.", 145m),
                ("Espresso bazlı", "Americano", "Çift espresso, sıcak su. Kahvenin yalın hali.", 110m),
                ("Filtre kahve", "Günün filtre kahvesi", "Taze demlenmiş, sade ve aromatik.", 100m),
                ("Soğuk kahveler", "Cold Brew", "Uzun sürede soğuk demlenmiş kahve.", 150m) })
            {
                if (!categories.TryGetValue(category, out var categoryEntity))
                {
                    categoryEntity = new Category { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = category, SortOrder = categories.Count };
                    categories.Add(category, categoryEntity);
                    db.Categories.Add(categoryEntity);
                }
                var product = new Product { Id = Guid.NewGuid(), TenantId = tenant.Id, CategoryId = categoryEntity.Id, Name = title, Description = description, BasePrice = price, SortOrder = db.ChangeTracker.Entries<Product>().Count() };
                db.Products.Add(product);
                db.BranchProducts.AddRange(new BranchProduct { TenantId = tenant.Id, BranchId = branch.Id, ProductId = product.Id, PriceOverride = slug == "demo-besiktas" ? price + 15 : null }, new BranchProduct { TenantId = tenant.Id, BranchId = second.Id, ProductId = product.Id, PriceOverride = price + 10 });
            }
        }
        await db.SaveChangesAsync();
        await SeedOptions(db);
        await PremiumDemoCatalog.Run(db);
        await PremiumDemoCatalog.Run(db, 3);
        await DemoMusicCatalog.Run(db);
        await DemoMusicCatalog.Run(db, 5);
    }

    private static async Task SeedOptions(AppDbContext db)
    {
        if (await db.DemoSeedMarkers.AnyAsync()) return;
        if (await db.OptionGroups.IgnoreQueryFilters().AnyAsync()) { db.DemoSeedMarkers.Add(new() { Id = 1 }); await db.SaveChangesAsync(); return; }
        // Upgrade existing demo catalogs without resetting prices, users or availability.
        var demoTenants = await db.Tenants.IgnoreQueryFilters().Where(x => x.Name == "Demo Kahve A" || x.Name == "Demo Kahve B").Select(x => x.Id).ToListAsync();
        var products = await db.Products.IgnoreQueryFilters().Where(x => demoTenants.Contains(x.TenantId)).ToListAsync();
        var seeded = await db.OptionGroups.IgnoreQueryFilters().Select(x => x.ProductId).Distinct().ToListAsync();
        foreach (var product in products.Where(x => !seeded.Contains(x.Id)))
        {
            void Group(string name, int min, int max, int sort, params (string Name, decimal Price)[] values)
            {
                var group = new OptionGroup { Id = Guid.NewGuid(), TenantId = product.TenantId, ProductId = product.Id, Name = name, MinSelect = min, MaxSelect = max, SortOrder = sort };
                db.OptionGroups.Add(group);
                foreach (var (value, index) in values.Select((value, index) => (value, index)))
                    db.OptionValues.Add(new OptionValue { Id = Guid.NewGuid(), TenantId = product.TenantId, OptionGroupId = group.Id, Name = value.Name, PriceDelta = value.Price, SortOrder = index });
            }
            Group("Boy", 1, 1, 0, ("Standart", 0m), ("Büyük", 25m));
            if (product.Name is "Flat White" or "Latte")
                Group("Süt", 1, 1, 1, ("Normal süt", 0m), ("Yulaf sütü", 20m), ("Laktozsuz süt", 10m));
            Group("Ekstralar", 0, 2, 2, ("Ek espresso", 30m), ("Vanilya şurubu", 15m), ("Karamel şurubu", 15m));
        }
        db.DemoSeedMarkers.Add(new() { Id = 1 });
        await db.SaveChangesAsync();
    }
}
