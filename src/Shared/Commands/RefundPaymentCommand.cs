using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;

namespace ECommerceOrderProcessing.Shared.Commands;

/// <summary>Instructs Payment Service to issue a Stripe refund. Sent by the Saga Orchestrator during compensation.</summary>
public sealed record RefundPaymentCommand : IRequest<ServiceResponse<bool>>
{
    public PaymentId PaymentId { get; init; }
    public OrderId OrderId { get; init; }
    public Money Amount { get; init; }
    public string Reason { get; init; }
    public Guid CorrelationId { get; init; }

    public RefundPaymentCommand(PaymentId paymentId, OrderId orderId, Money amount, string reason, Guid correlationId)
    {
        PaymentId = paymentId;
        OrderId = orderId;
        Amount = amount;
        Reason = reason;
        CorrelationId = correlationId;
    }
}
