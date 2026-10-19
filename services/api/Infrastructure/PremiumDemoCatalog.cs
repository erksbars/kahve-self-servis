using Kahve.Api.Modules.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Infrastructure;
public static class PremiumDemoCatalog
{
    public static async Task Run(AppDbContext db, int edition = 2)
    {
        if (edition is not (2 or 3)) throw new ArgumentOutOfRangeException(nameof(edition));
        if (await db.DemoSeedMarkers.AnyAsync(x => x.Id == edition)) return;
        var tenants = await db.Tenants.IgnoreQueryFilters().Where(x => x.Name == "Demo Kahve A" || x.Name == "Demo Kahve B").ToListAsync();
        foreach (var tenant in tenants)
        {
            var categories = await db.Categories.IgnoreQueryFilters().Where(x => x.TenantId == tenant.Id).ToListAsync();
            var products = await db.Products.IgnoreQueryFilters().Where(x => x.TenantId == tenant.Id).ToListAsync();
            var branches = await db.Branches.IgnoreQueryFilters().Where(x => x.TenantId == tenant.Id).ToListAsync();
            var entries = new[] {
                ("Espresso bazlı", "Flat White", "Çift espresso, kadifemsi süt. Dengeli ve yoğun.", 140m, "flat-white", true, true),
                ("Espresso bazlı", "Latte", "Espresso ve bol sıcak süt. Yumuşak bir başlangıç.", 145m, "latte", true, true),
                ("Espresso bazlı", "Americano", "Çift espresso, sıcak su. Kahvenin yalın hali.", 110m, "americano", false, true),
                ("Espresso bazlı", "Espresso", "Küçük fincanda yoğun aroma ve altın renkli krema.", 90m, "espresso", false, false),
                ("Espresso bazlı", "Cappuccino", "Espresso, sıcak süt ve hafif süt köpüğü.", 140m, "cappuccino", true, true),
                ("Soğuk kahveler", "Cold Brew", "Uzun sürede soğuk demlenmiş kahve.", 150m, "cold-brew", false, true),
                ("Soğuk kahveler", "Iced Latte", "Buz üzerinde espresso ve süt. Serin, yumuşak bir mola.", 155m, "iced-latte", true, true),
                ("Filtre kahve", "Günün filtre kahvesi", "Taze demlenmiş, sade ve aromatik.", 100m, "filter", false, true),
                ("Kahve dışı", "Matcha Latte", "Yeşil çay matcha ve sıcak süt. Yumuşak ve dengeli.", 175m, "matcha", true, true),
                ("Fırından", "Kruvasan", "Altın renkli, kat kat tereyağlı klasik kruvasan.", 120m, "croissant", false, false),
                ("Tatlılar", "Brownie", "Yoğun çikolatalı, içi yumuşak brownie.", 150m, "brownie", false, false),
                ("Tatlılar", "San Sebastian", "Karamelize yüzey, kremsi doku. Sade bir klasik.", 190m, "cheesecake", false, false) };
            if (edition == 3) entries = [..entries,
                ("Espresso bazlı", "Mocha", "Espresso, çikolata ve süt. Yoğun, yumuşak bir buluşma.", 165m, "mocha", true, true),
                ("Soğuk kahveler", "Iced Americano", "Espresso, soğuk su ve buz. Sade bir ferahlık.", 125m, "iced-americano", false, true),
                ("Soğuk kahveler", "Espresso Tonic", "Espresso, tonik ve buz. Narenciye dokunuşuyla farklı bir mola.", 180m, "espresso-tonic", false, false),
                ("Filtre kahve", "V60", "El demlemesi filtre kahve. Berrak ve dengeli bir fincan.", 170m, "v60", false, false),
                ("Filtre kahve", "Chemex", "El demlemesi kahve. Hafif gövdeli, temiz bir tat.", 185m, "chemex", false, false),
                ("Kahve dışı", "Chai Latte", "Baharatlı çay ve sıcak süt. Yumuşak, sıcak bir eşlikçi.", 170m, "chai", true, true),
                ("Kahve dışı", "Sıcak Çikolata", "Yoğun çikolata, sıcak süt ve hafif krema.", 160m, "hot-chocolate", true, true),
                ("Kahve dışı", "Çay", "İnce belli bardakta taze demlenmiş siyah çay.", 55m, "tea", false, false),
                ("Fırından", "Bademli Kruvasan", "Kat kat kruvasan, badem dolgusu ve file badem.", 165m, "almond-croissant", false, false),
                ("Fırından", "Cookie", "Altın renkli kenarlar, bol çikolata parçaları.", 95m, "cookie", false, false),
                ("Fırından", "Tarçınlı Çörek", "Tarçınlı spiral hamur ve hafif vanilyalı glaze.", 140m, "cinnamon-roll", false, false),
                ("Tatlılar", "Limonlu Cheesecake", "Limon kreması, yumuşak dolgu ve bisküvi tabanı.", 180m, "lemon-cheesecake", false, false)
            ];
            foreach (var (category, name, description, price, image, milk, size) in entries)
            {
                var product = products.SingleOrDefault(x => x.Name == name);
                if (product is not null)
                {
                    // Never overwrite an owner's edits or previously uploaded photograph.
                    if (product.Version == 1 && product.ImagePath is null) { product.ImagePath = $"/images/menu/{image}.webp"; product.ImageAlt = name; product.Version++; }
                    continue;
                }
                var c = categories.FirstOrDefault(x => x.Name == category);
                if (c is null) { c = new() { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = category, SortOrder = categories.Count }; categories.Add(c); db.Categories.Add(c); }
                product = new() { Id = Guid.NewGuid(), TenantId = tenant.Id, CategoryId = c.Id, Name = name, Description = description, BasePrice = price, SortOrder = products.Count == 0 ? 0 : products.Max(x => x.SortOrder) + 1, ImagePath = $"/images/menu/{image}.webp", ImageAlt = name };
                products.Add(product); db.Products.Add(product);
                foreach (var branch in branches) db.BranchProducts.Add(new() { TenantId = tenant.Id, BranchId = branch.Id, ProductId = product.Id, PriceOverride = branch.Slug.EndsWith("-2") ? price + 10 : tenant.Name == "Demo Kahve B" ? price + 15 : null });
                void Group(string label, int sort, params (string name, decimal price)[] values)
                {
                    var g = new OptionGroup { Id = Guid.NewGuid(), TenantId = tenant.Id, ProductId = product.Id, Name = label, MinSelect = 1, MaxSelect = 1, SortOrder = sort }; db.OptionGroups.Add(g);
                    foreach (var (v, i) in values.Select((v, i) => (v, i))) db.OptionValues.Add(new() { Id = Guid.NewGuid(), TenantId = tenant.Id, OptionGroupId = g.Id, Name = v.name, PriceDelta = v.price, SortOrder = i });
                }
                if (size) Group("Boy", 0, ("Standart", 0m), ("Büyük", 25m));
                if (milk) Group("Süt", 1, ("Normal süt", 0m), ("Yulaf sütü", 20m), ("Laktozsuz süt", 10m));
            }
        }
        db.DemoSeedMarkers.Add(new() { Id = edition }); await db.SaveChangesAsync();
    }
}

