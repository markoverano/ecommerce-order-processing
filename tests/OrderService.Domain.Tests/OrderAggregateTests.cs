using ECommerceOrderProcessing.Shared.Events.Order;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using OrderService.Domain.Aggregates;
using OrderService.Domain.Enums;
using OrderService.Domain.Exceptions;
using Xunit;

namespace OrderService.Domain.Tests;

public sealed class OrderAggregateTests
{
    private static readonly CustomerId SomeCustomer = new(Guid.NewGuid());
    private static readonly ShippingAddress SomeAddress = ShippingAddress.Create(
        "123 Main St", null, "Springfield", "IL", "62701", "US");
    private static readonly IReadOnlyList<OrderItemData> OneItem =
        new[] { new OrderItemData(new ProductId(Guid.NewGuid()), 2, Money.Create(15.00m, "USD")) };

    [Fact]
    public void Create_WithValidData_SetsStatusPending()
    {
        var order = Order.Create(SomeCustomer, OneItem, SomeAddress, Guid.NewGuid());

        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public void Create_WithValidData_RaisesOrderCreatedEvent()
    {
        var order = Order.Create(SomeCustomer, OneItem, SomeAddress, Guid.NewGuid());

        Assert.Single(order.UncommittedEvents);
        Assert.IsType<OrderCreated>(order.UncommittedEvents[0]);
    }

    [Fact]
    public void Create_WithValidData_ComputesTotalCorrectly()
    {
        var items = new[]
        {
            new OrderItemData(new ProductId(Guid.NewGuid()), 3, Money.Create(10.00m, "USD")),
            new OrderItemData(new ProductId(Guid.NewGuid()), 1, Money.Create(25.00m, "USD"))
        };

        var order = Order.Create(SomeCustomer, items, SomeAddress, Guid.NewGuid());

        Assert.Equal(Money.Create(55.00m, "USD"), order.TotalAmount);
    }

    [Fact]
    public void Create_WithEmptyItems_ThrowsInvalidOrderException()
    {
        Assert.Throws<InvalidOrderException>(() =>
            Order.Create(SomeCustomer, Array.Empty<OrderItemData>(), SomeAddress, Guid.NewGuid()));
    }

    [Fact]
    public void Create_AssignsNonEmptyId()
    {
        var order = Order.Create(SomeCustomer, OneItem, SomeAddress, Guid.NewGuid());

        Assert.NotEqual(Guid.Empty, order.Id);
    }

    [Fact]
    public void Confirm_FromPendingState_SetsStatusConfirmed()
    {
        var order = CreatePendingOrder();

        order.Confirm(Guid.NewGuid());

        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void Confirm_FromPendingState_RaisesOrderConfirmedEvent()
    {
        var order = CreatePendingOrder();

        order.Confirm(Guid.NewGuid());

        Assert.Contains(order.UncommittedEvents, e => e is OrderConfirmed);
    }

    [Fact]
    public void Confirm_FromConfirmedState_ThrowsInvalidOrderException()
    {
        var order = CreatePendingOrder();
        order.Confirm(Guid.NewGuid());
        order.ClearUncommittedEvents();

        Assert.Throws<InvalidOrderException>(() => order.Confirm(Guid.NewGuid()));
    }

    [Fact]
    public void Fail_FromPendingState_SetsStatusFailed()
    {
        var order = CreatePendingOrder();

        order.Fail("Payment declined", Guid.NewGuid());

        Assert.Equal(OrderStatus.Failed, order.Status);
    }

    [Fact]
    public void Fail_FromPendingState_RaisesOrderFailedEvent()
    {
        var order = CreatePendingOrder();

        order.Fail("Payment declined", Guid.NewGuid());

        Assert.Contains(order.UncommittedEvents, e => e is OrderFailed);
    }

    [Fact]
    public void Fail_FromConfirmedState_ThrowsInvalidOrderException()
    {
        var order = CreatePendingOrder();
        order.Confirm(Guid.NewGuid());
        order.ClearUncommittedEvents();

        Assert.Throws<InvalidOrderException>(() => order.Fail("reason", Guid.NewGuid()));
    }

    [Fact]
    public void Compensate_FromPendingState_SetsStatusCompensated()
    {
        var order = CreatePendingOrder();

        order.Compensate("Stock unavailable", Guid.NewGuid());

        Assert.Equal(OrderStatus.Compensated, order.Status);
    }

    [Fact]
    public void Compensate_FromPendingState_RaisesOrderCompensatedEvent()
    {
        var order = CreatePendingOrder();

        order.Compensate("Stock unavailable", Guid.NewGuid());

        Assert.Contains(order.UncommittedEvents, e => e is OrderCompensated);
    }

    [Fact]
    public void Rehydrate_FromEvents_ReconstructsStateCorrectly()
    {
        var original = Order.Create(SomeCustomer, OneItem, SomeAddress, Guid.NewGuid());
        var events = original.UncommittedEvents;

        var rehydrated = Order.Rehydrate(events);

        Assert.Equal(original.Id, rehydrated.Id);
        Assert.Equal(OrderStatus.Pending, rehydrated.Status);
        Assert.Equal(original.TotalAmount, rehydrated.TotalAmount);
    }

    [Fact]
    public void Rehydrate_FromEmptyEventList_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Order.Rehydrate(Array.Empty<ECommerceOrderProcessing.Shared.Domain.DomainEvent>()));
    }

    [Fact]
    public void ClearUncommittedEvents_RemovesAllEvents()
    {
        var order = Order.Create(SomeCustomer, OneItem, SomeAddress, Guid.NewGuid());

        order.ClearUncommittedEvents();

        Assert.Empty(order.UncommittedEvents);
    }

    private static Order CreatePendingOrder()
    {
        var order = Order.Create(SomeCustomer, OneItem, SomeAddress, Guid.NewGuid());
        order.ClearUncommittedEvents();
        return order;
    }
}
