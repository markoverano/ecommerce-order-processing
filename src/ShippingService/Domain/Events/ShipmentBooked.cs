using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ShippingService.Domain.Events;

// Internal event — persisted to the event store but never published to RabbitMQ.
// Captures the initial shipment request before FedEx confirmation.
public sealed record ShipmentBooked : DomainEvent
{
    public ShipmentId ShipmentId { get; init; }
    public OrderId OrderId { get; init; }
    public CustomerId CustomerId { get; init; }
    public ShippingAddress Destination { get; init; }
    public IReadOnlyList<ShipmentItem> Items { get; init; }

    public ShipmentBooked(
        ShipmentId shipmentId,
        OrderId orderId,
        CustomerId customerId,
        ShippingAddress destination,
        IReadOnlyList<ShipmentItem> items,
        int version,
        Guid correlationId)
        : base(shipmentId.Value, version, correlationId)
    {
        ShipmentId = shipmentId;
        OrderId = orderId;
        CustomerId = customerId;
        Destination = destination;
        Items = items;
    }
}
