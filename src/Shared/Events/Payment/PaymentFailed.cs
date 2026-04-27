using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Payment;

/// <summary>Published when Stripe declines the charge or returns a non-retryable error.</summary>
public sealed record PaymentFailed : DomainEvent
{
    public PaymentId PaymentId { get; init; }
    public OrderId OrderId { get; init; }
    public string Reason { get; init; }

    public PaymentFailed(PaymentId paymentId, OrderId orderId, string reason, int version, Guid correlationId)
        : base(paymentId.Value, version, correlationId)
    {
        PaymentId = paymentId;
        OrderId = orderId;
        Reason = reason;
    }
}
