using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Payment;

/// <summary>Published after Stripe processes a refund as part of saga compensation.</summary>
public sealed record PaymentRefunded : DomainEvent
{
    public PaymentId PaymentId { get; init; }
    public OrderId OrderId { get; init; }
    public Money Amount { get; init; }

    public PaymentRefunded(PaymentId paymentId, OrderId orderId, Money amount, int version, Guid correlationId)
        : base(paymentId.Value, version, correlationId)
    {
        PaymentId = paymentId;
        OrderId = orderId;
        Amount = amount;
    }
}
