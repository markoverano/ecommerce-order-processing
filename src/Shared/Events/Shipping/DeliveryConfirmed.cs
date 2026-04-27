using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Shipping;

/// <summary>Published when FedEx delivery webhook confirms the package was received by the customer.</summary>
public sealed record DeliveryConfirmed : DomainEvent
{
    public ShipmentId ShipmentId { get; init; }
    public OrderId OrderId { get; init; }
    public DateTimeOffset DeliveredAt { get; init; }

    public DeliveryConfirmed(ShipmentId shipmentId, OrderId orderId, DateTimeOffset deliveredAt, int version, Guid correlationId)
        : base(shipmentId.Value, version, correlationId)
    {
        ShipmentId = shipmentId;
        OrderId = orderId;
        DeliveredAt = deliveredAt;
    }
}
