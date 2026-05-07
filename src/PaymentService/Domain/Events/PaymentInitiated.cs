using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace PaymentService.Domain.Events;

// Internal event used only for event sourcing within the Payment aggregate.
// Not published to RabbitMQ; downstream services care only about PaymentProcessed/Failed/Refunded.
internal sealed record PaymentInitiated : DomainEvent
{
    public PaymentId PaymentId { get; init; }
    public OrderId OrderId { get; init; }
    public CustomerId CustomerId { get; init; }
    public Money Amount { get; init; }
    public string PaymentMethodId { get; init; }

    public PaymentInitiated(
        PaymentId paymentId,
        OrderId orderId,
        CustomerId customerId,
        Money amount,
        string paymentMethodId,
        int version,
        Guid correlationId)
        : base(paymentId.Value, version, correlationId)
    {
        PaymentId = paymentId;
        OrderId = orderId;
        CustomerId = customerId;
        Amount = amount;
        PaymentMethodId = paymentMethodId;
    }
}
