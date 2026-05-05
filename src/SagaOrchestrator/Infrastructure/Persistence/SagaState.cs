namespace SagaOrchestrator.Infrastructure.Persistence;

public sealed class SagaState
{
    public Guid SagaId { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string ShippingAddressJson { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = string.Empty;
    public Guid? PaymentId { get; set; }
    public decimal? PaymentAmount { get; set; }
    public string? PaymentCurrency { get; set; }
    public Guid? ReservationId { get; set; }
    public string? CompensationReason { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
