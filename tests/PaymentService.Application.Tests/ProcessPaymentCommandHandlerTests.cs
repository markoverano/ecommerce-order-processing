using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaymentService.Application.Commands;
using PaymentService.Application.ExternalClients;
using PaymentService.Application.Validation;
using PaymentService.Domain.Aggregates;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Exceptions;
using PaymentService.Domain.Repositories;
using Xunit;

namespace PaymentService.Application.Tests;

public sealed class ProcessPaymentCommandHandlerTests
{
    private static readonly OrderId SomeOrder = new(Guid.NewGuid());
    private static readonly CustomerId SomeCustomer = new(Guid.NewGuid());
    private static readonly Money HundredUsd = Money.Create(100m, "USD");

    private static ProcessPaymentCommand BuildCommand(string paymentMethodId = "pm_card_visa") =>
        new(SomeOrder, SomeCustomer, HundredUsd, paymentMethodId, Guid.NewGuid());

    [Fact]
    public async Task Handle_WhenStripeSucceeds_SavesProcessedPaymentAndReturnsId()
    {
        var repoMock = new Mock<IPaymentRepository>();
        var stripeMock = new Mock<IStripePaymentClient>();
        stripeMock
            .Setup(s => s.ChargeAsync(It.IsAny<string>(), It.IsAny<Money>(), It.IsAny<Guid>(), It.IsAny<OrderId>(), It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeChargeResult(true, "ch_testcharge", null));

        Payment? saved = null;
        repoMock
            .Setup(r => r.SaveAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((p, _) => saved = p)
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(repoMock.Object, stripeMock.Object);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(saved);
        Assert.Equal(PaymentStatus.Processed, saved!.Status);
    }

    [Fact]
    public async Task Handle_WhenStripeReturnsFailure_SavesFailedPaymentAndReturnsId()
    {
        var repoMock = new Mock<IPaymentRepository>();
        var stripeMock = new Mock<IStripePaymentClient>();
        stripeMock
            .Setup(s => s.ChargeAsync(It.IsAny<string>(), It.IsAny<Money>(), It.IsAny<Guid>(), It.IsAny<OrderId>(), It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeChargeResult(false, null, "Your card was declined."));

        Payment? saved = null;
        repoMock
            .Setup(r => r.SaveAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((p, _) => saved = p)
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(repoMock.Object, stripeMock.Object);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(saved);
        Assert.Equal(PaymentStatus.Failed, saved!.Status);
    }

    [Fact]
    public async Task Handle_WhenStripeThrowsPaymentProcessingException_SavesFailedPaymentAndReturnsId()
    {
        var repoMock = new Mock<IPaymentRepository>();
        var stripeMock = new Mock<IStripePaymentClient>();
        stripeMock
            .Setup(s => s.ChargeAsync(It.IsAny<string>(), It.IsAny<Money>(), It.IsAny<Guid>(), It.IsAny<OrderId>(), It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PaymentProcessingException("Card declined by issuer."));

        Payment? saved = null;
        repoMock
            .Setup(r => r.SaveAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((p, _) => saved = p)
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(repoMock.Object, stripeMock.Object);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Failed, saved!.Status);
    }

    [Fact]
    public async Task Handle_WithEmptyPaymentMethodId_ReturnsValidationFailure()
    {
        var repoMock = new Mock<IPaymentRepository>();
        var stripeMock = new Mock<IStripePaymentClient>();

        var handler = BuildHandler(repoMock.Object, stripeMock.Object);

        var result = await handler.Handle(BuildCommand(paymentMethodId: string.Empty), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_FAILED", result.Error!.Code);
        stripeMock.Verify(
            s => s.ChargeAsync(It.IsAny<string>(), It.IsAny<Money>(), It.IsAny<Guid>(),
                It.IsAny<OrderId>(), It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStripeSucceeds_ReturnedPaymentIdMatchesSavedPayment()
    {
        var repoMock = new Mock<IPaymentRepository>();
        var stripeMock = new Mock<IStripePaymentClient>();
        stripeMock
            .Setup(s => s.ChargeAsync(It.IsAny<string>(), It.IsAny<Money>(), It.IsAny<Guid>(), It.IsAny<OrderId>(), It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeChargeResult(true, "ch_testcharge", null));

        Payment? saved = null;
        repoMock
            .Setup(r => r.SaveAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((p, _) => saved = p)
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(repoMock.Object, stripeMock.Object);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.Equal(saved!.PaymentId, result.Data);
    }

    private static ProcessPaymentCommandHandler BuildHandler(
        IPaymentRepository repository,
        IStripePaymentClient stripe) =>
        new(repository, stripe, new ProcessPaymentCommandValidator(), NullLogger<ProcessPaymentCommandHandler>.Instance);
}
