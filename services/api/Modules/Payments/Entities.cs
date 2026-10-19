namespace Kahve.Api.Modules.Payments;

public sealed class Payment
{
    public string InitializationState { get; set; } = "NotStarted";
    public string ProtectedProviderToken { get; set; } = "";
    public string ProtectedPaymentUrl { get; set; } = "";
    public string ProviderResultJson { get; set; } = "{}";
    public string? LastProviderError { get; set; }
    public DateTimeOffset? LastVerifiedAt { get; set; }
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid OrderId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string RequestHash { get; set; } = "";
    public string Provider { get; set; } = "sandbox";
    public string ProviderReference { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string SessionTokenHash { get; set; } = "";
    public string ProtectedSessionToken { get; set; } = "";
}

public sealed class PaymentWebhookEvent
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string Provider { get; set; } = "sandbox";
    public string EventId { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
}

public sealed class PaymentOutbox
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string Type { get; set; } = "PaymentConfirmed";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public string? LastError { get; set; }
}
