using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Shipping;

/// <summary>Published when FedEx API accepts the shipment request and returns a tracking number.</summary>
public sealed record ShipmentCreated : DomainEvent
{
    public ShipmentId ShipmentId { get; init; }
    public OrderId OrderId { get; init; }
    public string TrackingNumber { get; init; }

    public ShipmentCreated(ShipmentId shipmentId, OrderId orderId, string trackingNumber, int version, Guid correlationId)
        : base(shipmentId.Value, version, correlationId)
    {
        ShipmentId = shipmentId;
        OrderId = orderId;
        TrackingNumber = trackingNumber;
    }
}
