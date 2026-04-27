using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Order;

/// <summary>Published after all compensation steps complete (refund issued, stock released).</summary>
public sealed record OrderCompensated : DomainEvent
{
    public OrderId OrderId { get; init; }
    public string Reason { get; init; }

    public OrderCompensated(OrderId orderId, int version, string reason, Guid correlationId)
        : base(orderId.Value, version, correlationId)
    {
        OrderId = orderId;
        Reason = reason;
    }
}
