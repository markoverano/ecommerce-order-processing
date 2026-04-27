using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Order;

/// <summary>Published when payment fails or another unrecoverable error occurs before inventory reservation.</summary>
public sealed record OrderFailed : DomainEvent
{
    public OrderId OrderId { get; init; }
    public string Reason { get; init; }

    public OrderFailed(OrderId orderId, int version, string reason, Guid correlationId)
        : base(orderId.Value, version, correlationId)
    {
        OrderId = orderId;
        Reason = reason;
    }
}
