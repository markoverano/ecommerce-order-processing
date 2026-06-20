namespace AnalyticsService.Domain.Entities;

public record OrderMetric
{
    public long Id { get; init; }
    public required Guid OrderId { get; init; }
    public Guid? CustomerId { get; set; }
    public required string Status { get; set; }
    public decimal OrderValue { get; set; }
    public string? CurrencyCode { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ConfirmedAt { get; set; }
    public int? FulfillmentDays { get; set; }

    public static OrderMetric Create(Guid orderId, Guid? customerId, decimal orderValue, string currencyCode = "USD")
    {
        return new OrderMetric
        {
            OrderId = orderId,
            CustomerId = customerId,
            Status = "Created",
            OrderValue = orderValue,
            CurrencyCode = currencyCode,
            CreatedAt = DateTime.UtcNow
        };
    }
}
