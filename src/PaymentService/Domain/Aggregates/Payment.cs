using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Payment;
using ECommerceOrderProcessing.Shared.ValueObjects;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Events;
using PaymentService.Domain.Exceptions;
using PaymentService.Domain.ValueObjects;

namespace PaymentService.Domain.Aggregates;

public sealed class Payment : AggregateRoot
{
    public PaymentId PaymentId => PaymentId.From(Id);
    public OrderId OrderId { get; private set; }
    public CustomerId CustomerId { get; private set; }
    public Money Amount { get; private set; }
    public string PaymentMethodId { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public StripeChargeId? ChargeId { get; private set; }

    private Payment() { }

    public static Payment Create(
        OrderId orderId,
        CustomerId customerId,
        Money amount,
        string paymentMethodId,
        Guid correlationId)
    {
        if (string.IsNullOrWhiteSpace(paymentMethodId))
            throw new PaymentProcessingException("PaymentMethodId is required.");

        var paymentId = PaymentId.New();
        var payment = new Payment();
        payment.RaiseEvent(new PaymentInitiated(paymentId, orderId, customerId, amount, paymentMethodId, 1, correlationId));
        return payment;
    }

    public void MarkAsProcessed(StripeChargeId chargeId, Guid correlationId)
    {
        if (Status != PaymentStatus.Pending)
            throw new PaymentProcessingException($"Cannot mark a {Status} payment as processed.");
        RaiseEvent(new PaymentProcessed(PaymentId, OrderId, Amount, chargeId.Value, Version + 1, correlationId));
    }

    public void MarkAsFailed(string reason, Guid correlationId)
    {
        if (Status != PaymentStatus.Pending)
            throw new PaymentProcessingException($"Cannot mark a {Status} payment as failed.");
        RaiseEvent(new PaymentFailed(PaymentId, OrderId, reason, Version + 1, correlationId));
    }

    public void MarkAsRefunded(Money amount, Guid correlationId)
    {
        if (Status != PaymentStatus.Processed)
            throw new PaymentProcessingException($"Only processed payments can be refunded. Current status: {Status}.");
        RaiseEvent(new PaymentRefunded(PaymentId, OrderId, amount, Version + 1, correlationId));
    }

    // Reconstructs aggregate state from a persisted event stream without raising new uncommitted events.
    public static Payment Rehydrate(IReadOnlyList<DomainEvent> events)
    {
        if (events.Count == 0)
            throw new InvalidOperationException("Cannot rehydrate a Payment from an empty event stream.");

        var payment = new Payment();
        foreach (var evt in events)
        {
            payment.Apply(evt);
            payment.Version++;
        }
        return payment;
    }

    protected override void Apply(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case PaymentInitiated e:
                Id = e.AggregateId;
                OrderId = e.OrderId;
                CustomerId = e.CustomerId;
                Amount = e.Amount;
                PaymentMethodId = e.PaymentMethodId;
                Status = PaymentStatus.Pending;
                break;
            case PaymentProcessed e:
                Status = PaymentStatus.Processed;
                ChargeId = StripeChargeId.From(e.StripeChargeId);
                break;
            case PaymentFailed:
                Status = PaymentStatus.Failed;
                break;
            case PaymentRefunded:
                Status = PaymentStatus.Refunded;
                break;
        }
    }
}
