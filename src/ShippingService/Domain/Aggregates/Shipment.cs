using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Shipping;
using ECommerceOrderProcessing.Shared.ValueObjects;
using ShippingService.Domain.Enums;
using ShippingService.Domain.Events;
using ShippingService.Domain.Exceptions;
using ShippingService.Domain.ValueObjects;

namespace ShippingService.Domain.Aggregates;

public sealed class Shipment : AggregateRoot
{
    public ShipmentId ShipmentId => ShipmentId.From(Id);
    public OrderId OrderId { get; private set; }
    public CustomerId CustomerId { get; private set; }
    public ShippingAddress Destination { get; private set; }
    public IReadOnlyList<ShipmentItem> Items { get; private set; } = Array.Empty<ShipmentItem>();
    public ShipmentStatus Status { get; private set; }
    public TrackingNumber? TrackingNumber { get; private set; }

    private Shipment() { }

    public static Shipment Create(
        OrderId orderId,
        CustomerId customerId,
        ShippingAddress destination,
        IReadOnlyList<ShipmentItem> items,
        Guid correlationId)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
            throw new ShipmentProcessingException("Shipment must contain at least one item.");

        var shipmentId = ShipmentId.New();
        var shipment = new Shipment();
        shipment.RaiseEvent(new ShipmentBooked(shipmentId, orderId, customerId, destination, items, 1, correlationId));
        return shipment;
    }

    public void MarkAsCreated(TrackingNumber trackingNumber, Guid correlationId)
    {
        if (Status != ShipmentStatus.Pending)
            throw new ShipmentProcessingException($"Cannot mark a {Status} shipment as created.");

        RaiseEvent(new ShipmentCreated(ShipmentId, OrderId, trackingNumber.Value, Version + 1, correlationId));
    }

    public void MarkAsFailed(string reason, Guid correlationId)
    {
        if (Status != ShipmentStatus.Pending)
            throw new ShipmentProcessingException($"Cannot mark a {Status} shipment as failed.");

        RaiseEvent(new ShipmentFailed(ShipmentId, OrderId, reason, Version + 1, correlationId));
    }

    public void Dispatch(DateTimeOffset dispatchedAt, Guid correlationId)
    {
        if (Status != ShipmentStatus.Created)
            throw new ShipmentProcessingException($"Cannot dispatch a shipment in {Status} status.");

        RaiseEvent(new ShipmentDispatched(ShipmentId, OrderId, TrackingNumber!.Value.Value, dispatchedAt, Version + 1, correlationId));
    }

    public void ConfirmDelivery(DateTimeOffset deliveredAt, Guid correlationId)
    {
        if (Status is not (ShipmentStatus.Dispatched or ShipmentStatus.InTransit))
            throw new ShipmentProcessingException($"Cannot confirm delivery of a shipment in {Status} status.");

        RaiseEvent(new DeliveryConfirmed(ShipmentId, OrderId, deliveredAt, Version + 1, correlationId));
    }

    public void Cancel(Guid correlationId)
    {
        if (Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled or ShipmentStatus.Failed)
            throw new ShipmentProcessingException($"Cannot cancel a shipment in {Status} status.");

        RaiseEvent(new ShipmentCancelled(ShipmentId, OrderId, Version + 1, correlationId));
    }

    // Reconstructs aggregate state from a persisted event stream without raising new uncommitted events.
    public static Shipment Rehydrate(IReadOnlyList<DomainEvent> events)
    {
        if (events.Count == 0)
            throw new InvalidOperationException("Cannot rehydrate a Shipment from an empty event stream.");

        var shipment = new Shipment();
        foreach (var evt in events)
        {
            shipment.Apply(evt);
            shipment.Version++;
        }
        return shipment;
    }

    protected override void Apply(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case ShipmentBooked e:
                Id = e.AggregateId;
                OrderId = e.OrderId;
                CustomerId = e.CustomerId;
                Destination = e.Destination;
                Items = e.Items;
                Status = ShipmentStatus.Pending;
                break;
            case ShipmentCreated e:
                Status = ShipmentStatus.Created;
                TrackingNumber = ValueObjects.TrackingNumber.From(e.TrackingNumber);
                break;
            case ShipmentFailed:
                Status = ShipmentStatus.Failed;
                break;
            case ShipmentDispatched:
                Status = ShipmentStatus.Dispatched;
                break;
            case DeliveryConfirmed:
                Status = ShipmentStatus.Delivered;
                break;
            case ShipmentCancelled:
                Status = ShipmentStatus.Cancelled;
                break;
        }
    }
}
