using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Order;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Domain.Exceptions;

namespace OrderService.Domain.Aggregates;

public sealed class Order : AggregateRoot
{
    public OrderId OrderId => OrderId.From(Id);
    public CustomerId CustomerId { get; private set; }

    private readonly List<OrderItem> _items = new();
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    public Money TotalAmount { get; private set; }
    public ShippingAddress ShippingAddress { get; private set; }
    public OrderStatus Status { get; private set; }

    private Order() { }

    public static Order Create(
        CustomerId customerId,
        IReadOnlyList<OrderItemData> items,
        ShippingAddress shippingAddress,
        Guid correlationId)
    {
        if (items.Count == 0)
            throw new InvalidOrderException("An order must contain at least one item.");

        var orderId = OrderId.New();
        var currency = items[0].UnitPrice.Currency;
        var total = items.Aggregate(
            Money.Zero(currency),
            (acc, item) => acc.Add(item.LineTotal));

        var order = new Order();
        order.RaiseEvent(new OrderCreated(orderId, customerId, items, total, shippingAddress, correlationId));
        return order;
    }

    public void Confirm(Guid correlationId)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOrderException($"Cannot confirm an order with status {Status}.");
        RaiseEvent(new OrderConfirmed(OrderId, Version + 1, correlationId));
    }

    public void Fail(string reason, Guid correlationId)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOrderException($"Cannot fail an order with status {Status}.");
        RaiseEvent(new OrderFailed(OrderId, Version + 1, reason, correlationId));
    }

    public void Compensate(string reason, Guid correlationId)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOrderException($"Cannot compensate an order with status {Status}.");
        RaiseEvent(new OrderCompensated(OrderId, Version + 1, reason, correlationId));
    }

    // Reconstructs aggregate state from a persisted event stream without raising new uncommitted events.
    public static Order Rehydrate(IReadOnlyList<DomainEvent> events)
    {
        if (events.Count == 0)
            throw new InvalidOperationException("Cannot rehydrate an Order from an empty event stream.");

        var order = new Order();
        foreach (var evt in events)
        {
            order.Apply(evt);
            order.Version++;
        }
        return order;
    }

    protected override void Apply(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case OrderCreated e:
                Id = e.AggregateId;
                CustomerId = e.CustomerId;
                _items.AddRange(e.Items.Select(i => OrderItem.Create(i.ProductId, i.Quantity, i.UnitPrice)));
                TotalAmount = e.TotalAmount;
                ShippingAddress = e.ShippingAddress;
                Status = OrderStatus.Pending;
                break;
            case OrderConfirmed:
                Status = OrderStatus.Confirmed;
                break;
            case OrderFailed:
                Status = OrderStatus.Failed;
                break;
            case OrderCompensated:
                Status = OrderStatus.Compensated;
                break;
        }
    }
}
