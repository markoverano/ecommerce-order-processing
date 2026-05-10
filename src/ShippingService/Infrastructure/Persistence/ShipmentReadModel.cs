namespace ShippingService.Infrastructure.Persistence;

public sealed class ShipmentReadModel
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
