namespace AnalyticsService.Domain.Entities;

public record PaymentMetric
{
    public long Id { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid PaymentId { get; init; }
    public required string Status { get; set; }
    public string? Gateway { get; set; }
    public decimal Amount { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime? RefundedAt { get; set; }

    public static PaymentMetric Create(Guid orderId, Guid paymentId, string status, string? gateway = null, decimal amount = 0)
    {
        return new PaymentMetric
        {
            OrderId = orderId,
            PaymentId = paymentId,
            Status = status,
            Gateway = gateway,
            Amount = amount
        };
    }
}
