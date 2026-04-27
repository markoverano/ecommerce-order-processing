using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Order;

/// <summary>Published when a new order is accepted for processing.</summary>
public sealed record OrderCreated : DomainEvent
{
    public OrderId OrderId { get; init; }
    public CustomerId CustomerId { get; init; }
    public IReadOnlyList<OrderItemData> Items { get; init; }
    public Money TotalAmount { get; init; }
    public ShippingAddress ShippingAddress { get; init; }

    public OrderCreated(
        OrderId orderId,
        CustomerId customerId,
        IReadOnlyList<OrderItemData> items,
        Money totalAmount,
        ShippingAddress shippingAddress,
        Guid correlationId)
        : base(orderId.Value, 1, correlationId)
    {
        OrderId = orderId;
        CustomerId = customerId;
        Items = items;
        TotalAmount = totalAmount;
        ShippingAddress = shippingAddress;
    }
}
