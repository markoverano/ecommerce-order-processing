using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Events.Shipping;
using ECommerceOrderProcessing.Shared.ValueObjects;
using ShippingService.Domain.Aggregates;
using ShippingService.Domain.Enums;
using ShippingService.Domain.Events;
using ShippingService.Domain.Exceptions;
using ShippingService.Domain.ValueObjects;
using Xunit;

namespace ShippingService.Domain.Tests;

public sealed class ShipmentAggregateTests
{
    private static readonly OrderId SomeOrder = new(Guid.NewGuid());
    private static readonly CustomerId SomeCustomer = new(Guid.NewGuid());
    private static readonly ShippingAddress SomeAddress = ShippingAddress.Create("123 Main St", null, "Springfield", "IL", "62701", "US");
    private static readonly IReadOnlyList<ShipmentItem> SomeItems = new[] { new ShipmentItem(new ProductId(Guid.NewGuid()), 2, "Widget") };
    private static readonly TrackingNumber SomeTracking = TrackingNumber.From("794644792798");

    [Fact]
    public void Create_WithValidData_SetsStatusPending()
    {
        var shipment = Shipment.Create(SomeOrder, SomeCustomer, SomeAddress, SomeItems, Guid.NewGuid());

        Assert.Equal(ShipmentStatus.Pending, shipment.Status);
    }

    [Fact]
    public void Create_WithValidData_RaisesShipmentBookedEvent()
    {
        var shipment = Shipment.Create(SomeOrder, SomeCustomer, SomeAddress, SomeItems, Guid.NewGuid());

        Assert.Single(shipment.UncommittedEvents);
        Assert.IsType<ShipmentBooked>(shipment.UncommittedEvents[0]);
    }

    [Fact]
    public void Create_AssignsNonEmptyId()
    {
        var shipment = Shipment.Create(SomeOrder, SomeCustomer, SomeAddress, SomeItems, Guid.NewGuid());

        Assert.NotEqual(Guid.Empty, shipment.Id);
    }

    [Fact]
    public void Create_WithEmptyItemList_ThrowsShipmentProcessingException()
    {
        Assert.Throws<ShipmentProcessingException>(() =>
            Shipment.Create(SomeOrder, SomeCustomer, SomeAddress, Array.Empty<ShipmentItem>(), Guid.NewGuid()));
    }

    [Fact]
    public void MarkAsCreated_FromPendingState_SetsStatusCreated()
    {
        var shipment = CreatePendingShipment();

        shipment.MarkAsCreated(SomeTracking, Guid.NewGuid());

        Assert.Equal(ShipmentStatus.Created, shipment.Status);
    }

    [Fact]
    public void MarkAsCreated_FromPendingState_RaisesShipmentCreatedEvent()
    {
        var shipment = CreatePendingShipment();

        shipment.MarkAsCreated(SomeTracking, Guid.NewGuid());

        Assert.Contains(shipment.UncommittedEvents, e => e is ShipmentCreated);
    }

    [Fact]
    public void MarkAsCreated_FromPendingState_StoresTrackingNumber()
    {
        var shipment = CreatePendingShipment();

        shipment.MarkAsCreated(SomeTracking, Guid.NewGuid());

        Assert.Equal(SomeTracking, shipment.TrackingNumber);
    }

    [Fact]
    public void MarkAsCreated_FromCreatedState_ThrowsShipmentProcessingException()
    {
        var shipment = CreateCreatedShipment();

        Assert.Throws<ShipmentProcessingException>(() =>
            shipment.MarkAsCreated(TrackingNumber.From("DIFFERENT123"), Guid.NewGuid()));
    }

    [Fact]
    public void MarkAsFailed_FromPendingState_SetsStatusFailed()
    {
        var shipment = CreatePendingShipment();

        shipment.MarkAsFailed("Address validation failed.", Guid.NewGuid());

        Assert.Equal(ShipmentStatus.Failed, shipment.Status);
    }

    [Fact]
    public void MarkAsFailed_FromPendingState_RaisesShipmentFailedEvent()
    {
        var shipment = CreatePendingShipment();

        shipment.MarkAsFailed("Service unavailable.", Guid.NewGuid());

        Assert.Contains(shipment.UncommittedEvents, e => e is ShipmentFailed);
    }

    [Fact]
    public void MarkAsFailed_FromCreatedState_ThrowsShipmentProcessingException()
    {
        var shipment = CreateCreatedShipment();

        Assert.Throws<ShipmentProcessingException>(() =>
            shipment.MarkAsFailed("reason", Guid.NewGuid()));
    }

    [Fact]
    public void Dispatch_FromCreatedState_SetsStatusDispatched()
    {
        var shipment = CreateCreatedShipment();

        shipment.Dispatch(DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Equal(ShipmentStatus.Dispatched, shipment.Status);
    }

    [Fact]
    public void Dispatch_FromCreatedState_RaisesShipmentDispatchedEvent()
    {
        var shipment = CreateCreatedShipment();

        shipment.Dispatch(DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Contains(shipment.UncommittedEvents, e => e is ShipmentDispatched);
    }

    [Fact]
    public void Dispatch_FromPendingState_ThrowsShipmentProcessingException()
    {
        var shipment = CreatePendingShipment();

        Assert.Throws<ShipmentProcessingException>(() =>
            shipment.Dispatch(DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void ConfirmDelivery_FromDispatchedState_SetsStatusDelivered()
    {
        var shipment = CreateDispatchedShipment();

        shipment.ConfirmDelivery(DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
    }

    [Fact]
    public void ConfirmDelivery_FromDispatchedState_RaisesDeliveryConfirmedEvent()
    {
        var shipment = CreateDispatchedShipment();

        shipment.ConfirmDelivery(DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Contains(shipment.UncommittedEvents, e => e is DeliveryConfirmed);
    }

    [Fact]
    public void ConfirmDelivery_FromPendingState_ThrowsShipmentProcessingException()
    {
        var shipment = CreatePendingShipment();

        Assert.Throws<ShipmentProcessingException>(() =>
            shipment.ConfirmDelivery(DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void Cancel_FromCreatedState_SetsStatusCancelled()
    {
        var shipment = CreateCreatedShipment();

        shipment.Cancel(Guid.NewGuid());

        Assert.Equal(ShipmentStatus.Cancelled, shipment.Status);
    }

    [Fact]
    public void Cancel_FromCreatedState_RaisesShipmentCancelledEvent()
    {
        var shipment = CreateCreatedShipment();

        shipment.Cancel(Guid.NewGuid());

        Assert.Contains(shipment.UncommittedEvents, e => e is ShipmentCancelled);
    }

    [Fact]
    public void Cancel_FromDeliveredState_ThrowsShipmentProcessingException()
    {
        var shipment = CreateDispatchedShipment();
        shipment.ConfirmDelivery(DateTimeOffset.UtcNow, Guid.NewGuid());
        shipment.ClearUncommittedEvents();

        Assert.Throws<ShipmentProcessingException>(() => shipment.Cancel(Guid.NewGuid()));
    }

    [Fact]
    public void Cancel_FromCancelledState_ThrowsShipmentProcessingException()
    {
        var shipment = CreateCreatedShipment();
        shipment.Cancel(Guid.NewGuid());
        shipment.ClearUncommittedEvents();

        Assert.Throws<ShipmentProcessingException>(() => shipment.Cancel(Guid.NewGuid()));
    }

    [Fact]
    public void Rehydrate_FromEvents_ReconstructsStateCorrectly()
    {
        var original = Shipment.Create(SomeOrder, SomeCustomer, SomeAddress, SomeItems, Guid.NewGuid());
        original.MarkAsCreated(SomeTracking, Guid.NewGuid());
        var events = original.UncommittedEvents;

        var rehydrated = Shipment.Rehydrate(events);

        Assert.Equal(original.Id, rehydrated.Id);
        Assert.Equal(ShipmentStatus.Created, rehydrated.Status);
        Assert.Equal(SomeTracking, rehydrated.TrackingNumber);
    }

    [Fact]
    public void Rehydrate_FromEmptyEventList_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Shipment.Rehydrate(Array.Empty<ECommerceOrderProcessing.Shared.Domain.DomainEvent>()));
    }

    [Fact]
    public void ClearUncommittedEvents_RemovesAllEvents()
    {
        var shipment = Shipment.Create(SomeOrder, SomeCustomer, SomeAddress, SomeItems, Guid.NewGuid());

        shipment.ClearUncommittedEvents();

        Assert.Empty(shipment.UncommittedEvents);
    }

    private static Shipment CreatePendingShipment()
    {
        var shipment = Shipment.Create(SomeOrder, SomeCustomer, SomeAddress, SomeItems, Guid.NewGuid());
        shipment.ClearUncommittedEvents();
        return shipment;
    }

    private static Shipment CreateCreatedShipment()
    {
        var shipment = CreatePendingShipment();
        shipment.MarkAsCreated(SomeTracking, Guid.NewGuid());
        shipment.ClearUncommittedEvents();
        return shipment;
    }

    private static Shipment CreateDispatchedShipment()
    {
        var shipment = CreateCreatedShipment();
        shipment.Dispatch(DateTimeOffset.UtcNow, Guid.NewGuid());
        shipment.ClearUncommittedEvents();
        return shipment;
    }
}
