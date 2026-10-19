namespace Kahve.Api.Modules.Staff;

public sealed class StaffUser
{
    public int Version { get; set; } = 1;
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Barista";
    public bool Active { get; set; } = true;
}

public sealed class StaffInvitation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid StaffId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RedeemedAt { get; set; }
}

public sealed class StaffBranchAccess
{
    public Guid TenantId { get; set; }
    public Guid StaffId { get; set; }
    public Guid BranchId { get; set; }
}
