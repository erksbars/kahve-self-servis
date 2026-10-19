namespace Kahve.Api.Modules.Catalog;

public sealed class Product
{
    public string? ImagePath { get; set; }
    public string? ImageAlt { get; set; }
    public int Version { get; set; } = 1;
    public decimal TaxRate { get; set; }
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal BasePrice { get; set; }
    public bool Active { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class Category
{
    public int Version { get; set; } = 1;
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class OptionGroup
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public string Name { get; set; } = "";
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; } = 1;
    public int SortOrder { get; set; }
}

public sealed class OptionValue
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OptionGroupId { get; set; }
    public string Name { get; set; } = "";
    public decimal PriceDelta { get; set; }
    public bool Available { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class BranchOption
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid OptionValueId { get; set; }
    public decimal? PriceOverride { get; set; }
    public bool Available { get; set; } = true;
}

public sealed class BranchProduct
{
    public int Version { get; set; } = 1;
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public decimal? PriceOverride { get; set; }
    public bool Available { get; set; } = true;
}
