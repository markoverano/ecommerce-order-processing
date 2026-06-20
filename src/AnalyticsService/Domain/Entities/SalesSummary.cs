namespace AnalyticsService.Domain.Entities;

public record SalesSummary
{
    public long Id { get; init; }
    public required DateOnly Date { get; init; }
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }

    public static SalesSummary Create(DateOnly date)
    {
        return new SalesSummary
        {
            Date = date,
            TotalOrders = 0,
            TotalRevenue = 0,
            AverageOrderValue = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void UpdateRevenue(decimal amount, int orderCount)
    {
        TotalRevenue += amount;
        TotalOrders += orderCount;
        if (TotalOrders > 0)
            AverageOrderValue = TotalRevenue / TotalOrders;
        UpdatedAt = DateTime.UtcNow;
    }
}
