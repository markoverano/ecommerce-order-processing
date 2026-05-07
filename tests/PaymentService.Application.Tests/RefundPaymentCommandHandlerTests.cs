using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Events.Payment;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaymentService.Application.Commands;
using PaymentService.Application.ExternalClients;
using PaymentService.Domain.Aggregates;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Exceptions;
using PaymentService.Domain.Repositories;
using PaymentService.Domain.ValueObjects;
using Xunit;

namespace PaymentService.Application.Tests;

public sealed class RefundPaymentCommandHandlerTests
{
    private static readonly OrderId SomeOrder = new(Guid.NewGuid());
    private static readonly CustomerId SomeCustomer = new(Guid.NewGuid());
    private static readonly Money HundredUsd = Money.Create(100m, "USD");
    private static readonly StripeChargeId SomeCharge = StripeChargeId.From("ch_3test123");

    [Fact]
    public async Task Handle_WhenPaymentNotFound_ReturnsNotFoundFailure()
    {
        var repoMock = new Mock<IPaymentRepository>();
        repoMock.Setup(r => r.GetByIdAsync(It.IsAny<PaymentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        var handler = BuildHandler(repoMock.Object, new Mock<IStripePaymentGateway>().Object);
        var command = BuildRefundCommand(PaymentId.New());

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("PAYMENT_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenStripeRefundSucceeds_SavesRefundedPayment()
    {
        var payment = BuildProcessedPayment();
        var repoMock = new Mock<IPaymentRepository>();
        repoMock.Setup(r => r.GetByIdAsync(payment.PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var stripeMock = new Mock<IStripePaymentGateway>();
        stripeMock.Setup(s => s.RefundAsync(It.IsAny<string>(), It.IsAny<Money>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeRefundResult(true, null));

        Payment? saved = null;
        repoMock.Setup(r => r.SaveAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((p, _) => saved = p)
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(repoMock.Object, stripeMock.Object);

        var result = await handler.Handle(BuildRefundCommand(payment.PaymentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Refunded, saved!.Status);
        Assert.Contains(saved.UncommittedEvents, e => e is PaymentRefunded);
    }

    [Fact]
    public async Task Handle_WhenStripeRefundFails_ReturnsFailureWithoutSaving()
    {
        var payment = BuildProcessedPayment();
        var repoMock = new Mock<IPaymentRepository>();
        repoMock.Setup(r => r.GetByIdAsync(payment.PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var stripeMock = new Mock<IStripePaymentGateway>();
        stripeMock.Setup(s => s.RefundAsync(It.IsAny<string>(), It.IsAny<Money>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeRefundResult(false, "Refund declined."));

        var handler = BuildHandler(repoMock.Object, stripeMock.Object);

        var result = await handler.Handle(BuildRefundCommand(payment.PaymentId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("REFUND_FAILED", result.Error!.Code);
        repoMock.Verify(r => r.SaveAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStripeThrowsPaymentProcessingException_ReturnsFailure()
    {
        var payment = BuildProcessedPayment();
        var repoMock = new Mock<IPaymentRepository>();
        repoMock.Setup(r => r.GetByIdAsync(payment.PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var stripeMock = new Mock<IStripePaymentGateway>();
        stripeMock.Setup(s => s.RefundAsync(It.IsAny<string>(), It.IsAny<Money>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PaymentProcessingException("Refund rejected by Stripe."));

        var handler = BuildHandler(repoMock.Object, stripeMock.Object);

        var result = await handler.Handle(BuildRefundCommand(payment.PaymentId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("REFUND_FAILED", result.Error!.Code);
    }

    private static Payment BuildProcessedPayment()
    {
        var payment = Payment.Create(SomeOrder, SomeCustomer, HundredUsd, "pm_card_visa", Guid.NewGuid());
        payment.MarkAsProcessed(SomeCharge, Guid.NewGuid());
        payment.ClearUncommittedEvents();
        return payment;
    }

    private static RefundPaymentCommand BuildRefundCommand(PaymentId paymentId) =>
        new(paymentId, SomeOrder, HundredUsd, "Customer requested refund.", Guid.NewGuid());

    private static RefundPaymentCommandHandler BuildHandler(
        IPaymentRepository repository,
        IStripePaymentGateway stripe) =>
        new(repository, stripe, NullLogger<RefundPaymentCommandHandler>.Instance);
}
