using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;

namespace ECommerceOrderProcessing.Shared.Commands;

/// <summary>Instructs Payment Service to charge the customer via Stripe. Sent by the Saga Orchestrator.</summary>
public sealed record ProcessPaymentCommand : IRequest<ServiceResponse<PaymentId>>
{
    public OrderId OrderId { get; init; }
    public CustomerId CustomerId { get; init; }
    public Money Amount { get; init; }
    public string PaymentMethodId { get; init; }
    public Guid CorrelationId { get; init; }

    public ProcessPaymentCommand(OrderId orderId, CustomerId customerId, Money amount, string paymentMethodId, Guid correlationId)
    {
        OrderId = orderId;
        CustomerId = customerId;
        Amount = amount;
        PaymentMethodId = paymentMethodId;
        CorrelationId = correlationId;
    }
}
