using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Catalog;

public sealed record MenuBranch(Guid Id, string Name, string Slug, string Address, bool IsAcceptingOrders);
public sealed record MenuOption(Guid Id, string Name, decimal PriceDelta, bool Available);
public sealed record MenuGroup(Guid Id, string Name, int MinSelect, int MaxSelect, List<MenuOption> Values);
public sealed record MenuProduct(Guid Id, string Name, string Description, string Category, decimal Price, bool Available, List<MenuGroup> OptionGroups, decimal TaxRate = 0, string? ImageUrl = null, string? ImageAlt = null);
public sealed record MenuDto(MenuBranch Branch, string Merchant, string Currency, bool Demo, List<MenuProduct> Products, bool CheckoutEnabled = true, string CheckoutProvider = "sandbox");

public static class CatalogReader
{
    public static async Task<MenuDto?> Read(AppDbContext db, string slug, bool demo, CancellationToken ct)
    {
        // Anonymous catalog queries are explicitly constrained to the resolved branch + tenant.
        var branch = await db.Branches.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug, ct);
        if (branch is null) return null;
        var tenant = await db.Tenants.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == branch.TenantId, ct);
        var products = await (from bp in db.BranchProducts.IgnoreQueryFilters().AsNoTracking()
            join p in db.Products.IgnoreQueryFilters().AsNoTracking() on bp.ProductId equals p.Id
            join c in db.Categories.IgnoreQueryFilters().AsNoTracking() on p.CategoryId equals c.Id
            where bp.BranchId == branch.Id && bp.TenantId == branch.TenantId && p.TenantId == branch.TenantId && c.TenantId == branch.TenantId && p.Active
            orderby c.SortOrder, p.SortOrder, p.Name
            select new { p.Id, p.Name, p.Description, p.TaxRate, p.ImagePath, p.ImageAlt, Category = c.Name, Price = bp.PriceOverride ?? p.BasePrice, bp.Available }).ToListAsync(ct);
        var ids = products.Select(x => x.Id).ToList();
        var groups = await db.OptionGroups.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == branch.TenantId && ids.Contains(x.ProductId)).OrderBy(x => x.SortOrder).ToListAsync(ct);
        var groupIds = groups.Select(x => x.Id).ToList();
        var values = await db.OptionValues.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == branch.TenantId && groupIds.Contains(x.OptionGroupId)).OrderBy(x => x.SortOrder).ToListAsync(ct);
        var overrides = await db.BranchOptions.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == branch.TenantId && x.BranchId == branch.Id).ToDictionaryAsync(x => x.OptionValueId, ct);
        return new MenuDto(new(branch.Id, branch.Name, branch.Slug, branch.Address, branch.IsAcceptingOrders && BranchHours.IsOpen(branch, DateTimeOffset.UtcNow)), tenant.Name, "TRY", demo,
            products.Select(p => new MenuProduct(p.Id, p.Name, p.Description, p.Category, p.Price, p.Available,
                groups.Where(g => g.ProductId == p.Id).Select(g => new MenuGroup(g.Id, g.Name, g.MinSelect, g.MaxSelect,
                    values.Where(v => v.OptionGroupId == g.Id).Select(v =>
                    {
                        overrides.TryGetValue(v.Id, out var branchOption);
                        return new MenuOption(v.Id, v.Name, branchOption?.PriceOverride ?? v.PriceDelta, v.Available && (branchOption?.Available ?? true));
                    }).ToList())).ToList(), p.TaxRate, p.ImagePath, p.ImageAlt)).ToList());
    }
}
