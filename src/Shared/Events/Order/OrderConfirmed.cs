using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Order;

/// <summary>Published when a saga completes successfully — payment, inventory, shipping, and notification all succeeded.</summary>
public sealed record OrderConfirmed : DomainEvent
{
    public OrderId OrderId { get; init; }

    public OrderConfirmed(OrderId orderId, int version, Guid correlationId)
        : base(orderId.Value, version, correlationId)
    {
        OrderId = orderId;
    }
}
