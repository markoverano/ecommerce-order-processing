using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Shipping;

/// <summary>Published after successfully cancelling a shipment with the carrier.</summary>
public sealed record ShipmentCancelled : DomainEvent
{
    public ShipmentId ShipmentId { get; init; }
    public OrderId OrderId { get; init; }

    public ShipmentCancelled(ShipmentId shipmentId, OrderId orderId, int version, Guid correlationId)
        : base(shipmentId.Value, version, correlationId)
    {
        ShipmentId = shipmentId;
        OrderId = orderId;
    }
}
