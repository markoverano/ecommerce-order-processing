using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Shipping;

/// <summary>Published when FedEx dispatched webhook confirms the package left the facility.</summary>
public sealed record ShipmentDispatched : DomainEvent
{
    public ShipmentId ShipmentId { get; init; }
    public OrderId OrderId { get; init; }
    public string TrackingNumber { get; init; }
    public DateTimeOffset DispatchedAt { get; init; }

    public ShipmentDispatched(ShipmentId shipmentId, OrderId orderId, string trackingNumber, DateTimeOffset dispatchedAt, int version, Guid correlationId)
        : base(shipmentId.Value, version, correlationId)
    {
        ShipmentId = shipmentId;
        OrderId = orderId;
        TrackingNumber = trackingNumber;
        DispatchedAt = dispatchedAt;
    }
}
