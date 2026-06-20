namespace AnalyticsService.Domain.Entities;

public record ShippingMetric
{
    public long Id { get; init; }
    public required Guid ShipmentId { get; init; }
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? Destination { get; set; }

    public static ShippingMetric Create(Guid shipmentId, string? carrier = null, string? trackingNumber = null)
    {
        return new ShippingMetric
        {
            ShipmentId = shipmentId,
            Carrier = carrier,
            TrackingNumber = trackingNumber,
            CreatedAt = DateTime.UtcNow
        };
    }
}
