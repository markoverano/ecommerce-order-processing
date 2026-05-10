namespace ShippingService.Domain.Enums;

public enum ShipmentStatus
{
    Pending,
    Created,
    Failed,
    Dispatched,
    InTransit,
    Delivered,
    Cancelled
}
