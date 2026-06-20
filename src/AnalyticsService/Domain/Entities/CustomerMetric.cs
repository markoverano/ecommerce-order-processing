namespace AnalyticsService.Domain.Entities;

public record CustomerMetric
{
    public long Id { get; init; }
    public required Guid CustomerId { get; init; }
    public decimal LifetimeValue { get; set; }
    public int OrderCount { get; set; }
    public DateTime? FirstOrderAt { get; set; }
    public DateTime? LastOrderAt { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal RepeatRate { get; set; }

    public static CustomerMetric Create(Guid customerId)
    {
        return new CustomerMetric
        {
            CustomerId = customerId,
            LifetimeValue = 0,
            OrderCount = 0,
            AverageOrderValue = 0,
            RepeatRate = 0
        };
    }
}
