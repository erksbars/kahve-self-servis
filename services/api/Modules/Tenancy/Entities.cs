namespace Kahve.Api.Modules.Tenancy;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class Branch
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Address { get; set; } = "";
    public string TimeZone { get; set; } = "Europe/Istanbul";
    public bool IsAcceptingOrders { get; set; } = true;
    public int Version { get; set; } = 1;
    public int PreparationCapacity { get; set; } = 30;
    public string HoursJson { get; set; } = "[]";
}
