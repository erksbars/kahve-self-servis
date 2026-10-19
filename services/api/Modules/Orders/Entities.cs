namespace Kahve.Api.Modules.Orders;

public sealed class Order
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public long PublicNumber { get; set; }
    public string TrackingTokenHash { get; set; } = "";
    public string ProtectedTrackingToken { get; set; } = "";
    public string Status { get; set; } = "PaymentPending";
    public string PaymentStatus { get; set; } = "Pending";
    public decimal Total { get; set; }
    public string Currency { get; set; } = "TRY";
    public string ItemsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class OrderStatusHistory
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = "";
    public string Actor { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
