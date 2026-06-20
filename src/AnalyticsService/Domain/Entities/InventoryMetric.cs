namespace AnalyticsService.Domain.Entities;

public record InventoryMetric
{
    public long Id { get; init; }
    public required Guid ProductId { get; init; }
    public required Guid ReservationId { get; init; }
    public int QuantityReserved { get; set; }
    public int? DurationHours { get; set; }
    public DateTime? ReleasedAt { get; set; }

    public static InventoryMetric Create(Guid productId, Guid reservationId, int quantityReserved)
    {
        return new InventoryMetric
        {
            ProductId = productId,
            ReservationId = reservationId,
            QuantityReserved = quantityReserved
        };
    }
}
