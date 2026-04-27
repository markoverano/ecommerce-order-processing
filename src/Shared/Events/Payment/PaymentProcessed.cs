using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Payment;

/// <summary>Published by Payment Service after Stripe confirms a successful charge.</summary>
public sealed record PaymentProcessed : DomainEvent
{
    public PaymentId PaymentId { get; init; }
    public OrderId OrderId { get; init; }
    public Money Amount { get; init; }
    public string StripeChargeId { get; init; }

    public PaymentProcessed(PaymentId paymentId, OrderId orderId, Money amount, string stripeChargeId, int version, Guid correlationId)
        : base(paymentId.Value, version, correlationId)
    {
        PaymentId = paymentId;
        OrderId = orderId;
        Amount = amount;
        StripeChargeId = stripeChargeId;
    }
}
