using ECommerceOrderProcessing.Shared.Events.Payment;
using ECommerceOrderProcessing.Shared.ValueObjects;
using PaymentService.Domain.Aggregates;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Events;
using PaymentService.Domain.Exceptions;
using PaymentService.Domain.ValueObjects;
using Xunit;

namespace PaymentService.Domain.Tests;

public sealed class PaymentAggregateTests
{
    private static readonly OrderId SomeOrder = new(Guid.NewGuid());
    private static readonly CustomerId SomeCustomer = new(Guid.NewGuid());
    private static readonly Money HundredUsd = Money.Create(100.00m, "USD");
    private const string SomePaymentMethod = "pm_card_visa";
    private static readonly StripeChargeId SomeCharge = StripeChargeId.From("ch_3test123");

    [Fact]
    public void Create_WithValidData_SetsStatusPending()
    {
        var payment = Payment.Create(SomeOrder, SomeCustomer, HundredUsd, SomePaymentMethod, Guid.NewGuid());

        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void Create_WithValidData_RaisesPaymentInitiatedEvent()
    {
        var payment = Payment.Create(SomeOrder, SomeCustomer, HundredUsd, SomePaymentMethod, Guid.NewGuid());

        Assert.Single(payment.UncommittedEvents);
        Assert.IsType<PaymentInitiated>(payment.UncommittedEvents[0]);
    }

    [Fact]
    public void Create_AssignsNonEmptyId()
    {
        var payment = Payment.Create(SomeOrder, SomeCustomer, HundredUsd, SomePaymentMethod, Guid.NewGuid());

        Assert.NotEqual(Guid.Empty, payment.Id);
    }

    [Fact]
    public void Create_WithEmptyPaymentMethodId_ThrowsPaymentProcessingException()
    {
        Assert.Throws<PaymentProcessingException>(() =>
            Payment.Create(SomeOrder, SomeCustomer, HundredUsd, string.Empty, Guid.NewGuid()));
    }

    [Fact]
    public void MarkAsProcessed_FromPendingState_SetsStatusProcessed()
    {
        var payment = CreatePendingPayment();

        payment.MarkAsProcessed(SomeCharge, Guid.NewGuid());

        Assert.Equal(PaymentStatus.Processed, payment.Status);
    }

    [Fact]
    public void MarkAsProcessed_FromPendingState_RaisesPaymentProcessedEvent()
    {
        var payment = CreatePendingPayment();

        payment.MarkAsProcessed(SomeCharge, Guid.NewGuid());

        Assert.Contains(payment.UncommittedEvents, e => e is PaymentProcessed);
    }

    [Fact]
    public void MarkAsProcessed_FromPendingState_StoresChargeId()
    {
        var payment = CreatePendingPayment();

        payment.MarkAsProcessed(SomeCharge, Guid.NewGuid());

        Assert.Equal(SomeCharge, payment.ChargeId);
    }

    [Fact]
    public void MarkAsProcessed_FromProcessedState_ThrowsPaymentProcessingException()
    {
        var payment = CreatePendingPayment();
        payment.MarkAsProcessed(SomeCharge, Guid.NewGuid());
        payment.ClearUncommittedEvents();

        Assert.Throws<PaymentProcessingException>(() =>
            payment.MarkAsProcessed(StripeChargeId.From("ch_different"), Guid.NewGuid()));
    }

    [Fact]
    public void MarkAsFailed_FromPendingState_SetsStatusFailed()
    {
        var payment = CreatePendingPayment();

        payment.MarkAsFailed("Card declined.", Guid.NewGuid());

        Assert.Equal(PaymentStatus.Failed, payment.Status);
    }

    [Fact]
    public void MarkAsFailed_FromPendingState_RaisesPaymentFailedEvent()
    {
        var payment = CreatePendingPayment();

        payment.MarkAsFailed("Insufficient funds.", Guid.NewGuid());

        Assert.Contains(payment.UncommittedEvents, e => e is PaymentFailed);
    }

    [Fact]
    public void MarkAsFailed_FromProcessedState_ThrowsPaymentProcessingException()
    {
        var payment = CreatePendingPayment();
        payment.MarkAsProcessed(SomeCharge, Guid.NewGuid());
        payment.ClearUncommittedEvents();

        Assert.Throws<PaymentProcessingException>(() =>
            payment.MarkAsFailed("reason", Guid.NewGuid()));
    }

    [Fact]
    public void MarkAsRefunded_FromProcessedState_SetsStatusRefunded()
    {
        var payment = CreateProcessedPayment();

        payment.MarkAsRefunded(HundredUsd, Guid.NewGuid());

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void MarkAsRefunded_FromProcessedState_RaisesPaymentRefundedEvent()
    {
        var payment = CreateProcessedPayment();

        payment.MarkAsRefunded(HundredUsd, Guid.NewGuid());

        Assert.Contains(payment.UncommittedEvents, e => e is PaymentRefunded);
    }

    [Fact]
    public void MarkAsRefunded_FromPendingState_ThrowsPaymentProcessingException()
    {
        var payment = CreatePendingPayment();

        Assert.Throws<PaymentProcessingException>(() =>
            payment.MarkAsRefunded(HundredUsd, Guid.NewGuid()));
    }

    [Fact]
    public void MarkAsRefunded_FromFailedState_ThrowsPaymentProcessingException()
    {
        var payment = CreatePendingPayment();
        payment.MarkAsFailed("declined", Guid.NewGuid());
        payment.ClearUncommittedEvents();

        Assert.Throws<PaymentProcessingException>(() =>
            payment.MarkAsRefunded(HundredUsd, Guid.NewGuid()));
    }

    [Fact]
    public void Rehydrate_FromEvents_ReconstructsStateCorrectly()
    {
        var original = Payment.Create(SomeOrder, SomeCustomer, HundredUsd, SomePaymentMethod, Guid.NewGuid());
        original.MarkAsProcessed(SomeCharge, Guid.NewGuid());
        var events = original.UncommittedEvents;

        var rehydrated = Payment.Rehydrate(events);

        Assert.Equal(original.Id, rehydrated.Id);
        Assert.Equal(PaymentStatus.Processed, rehydrated.Status);
        Assert.Equal(SomeCharge, rehydrated.ChargeId);
    }

    [Fact]
    public void Rehydrate_FromEmptyEventList_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Payment.Rehydrate(Array.Empty<ECommerceOrderProcessing.Shared.Domain.DomainEvent>()));
    }

    [Fact]
    public void ClearUncommittedEvents_RemovesAllEvents()
    {
        var payment = Payment.Create(SomeOrder, SomeCustomer, HundredUsd, SomePaymentMethod, Guid.NewGuid());

        payment.ClearUncommittedEvents();

        Assert.Empty(payment.UncommittedEvents);
    }

    private static Payment CreatePendingPayment()
    {
        var payment = Payment.Create(SomeOrder, SomeCustomer, HundredUsd, SomePaymentMethod, Guid.NewGuid());
        payment.ClearUncommittedEvents();
        return payment;
    }

    private static Payment CreateProcessedPayment()
    {
        var payment = CreatePendingPayment();
        payment.MarkAsProcessed(SomeCharge, Guid.NewGuid());
        payment.ClearUncommittedEvents();
        return payment;
    }
}
