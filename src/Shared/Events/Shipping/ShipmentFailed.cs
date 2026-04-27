using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Shipping;

/// <summary>Published when FedEx API returns an error and the shipment cannot be created.</summary>
public sealed record ShipmentFailed : DomainEvent
{
    public ShipmentId ShipmentId { get; init; }
    public OrderId OrderId { get; init; }
    public string Reason { get; init; }

    public ShipmentFailed(ShipmentId shipmentId, OrderId orderId, string reason, int version, Guid correlationId)
        : base(shipmentId.Value, version, correlationId)
    {
        ShipmentId = shipmentId;
        OrderId = orderId;
        Reason = reason;
    }
}
