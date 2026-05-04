using ECommerceOrderProcessing.Shared.Events.Saga;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using SagaOrchestrator.Domain.Aggregates;
using SagaOrchestrator.Domain.Enums;
using SagaOrchestrator.Domain.Exceptions;
using Xunit;

namespace SagaOrchestrator.Domain.Tests;

public sealed class OrderProcessingSagaTests
{
    private static readonly OrderId SomeOrderId = OrderId.New();
    private static readonly CustomerId SomeCustomer = new(Guid.NewGuid());
    private static readonly Money SomeAmount = Money.Create(99.00m, "USD");
    private static readonly ShippingAddress SomeAddress = ShippingAddress.Create(
        "123 Main St", null, "Springfield", "IL", "62701", "US");
    private static readonly IReadOnlyList<OrderItemData> OneItem =
        new[] { new OrderItemData(new ProductId(Guid.NewGuid()), 2, Money.Create(49.50m, "USD")) };

    [Fact]
    public void Start_WithValidData_SetsStatusRunning()
    {
        var saga = CreateStartedSaga();

        Assert.Equal(SagaStatus.Running, saga.Status);
    }

    [Fact]
    public void Start_WithValidData_SetsStepPaymentPending()
    {
        var saga = CreateStartedSaga();

        Assert.Equal(SagaStep.PaymentPending, saga.CurrentStep);
    }

    [Fact]
    public void Start_RaisesSagaStartedEvent()
    {
        var saga = CreateStartedSaga();

        Assert.Single(saga.UncommittedEvents);
        Assert.IsType<SagaStarted>(saga.UncommittedEvents[0]);
    }

    [Fact]
    public void Start_AssignsNonEmptyId()
    {
        var saga = CreateStartedSaga();

        Assert.NotEqual(Guid.Empty, saga.Id);
    }

    [Fact]
    public void OnPaymentProcessed_FromPaymentPending_AdvancesToInventoryPending()
    {
        var saga = CreateStartedSaga();
        saga.ClearUncommittedEvents();

        saga.OnPaymentProcessed(PaymentId.New(), SomeAmount, Guid.NewGuid());

        Assert.Equal(SagaStep.InventoryPending, saga.CurrentStep);
        Assert.Equal(SagaStatus.Running, saga.Status);
    }

    [Fact]
    public void OnPaymentProcessed_StoresPaymentIdAndAmount()
    {
        var saga = CreateStartedSaga();
        var paymentId = PaymentId.New();

        saga.OnPaymentProcessed(paymentId, SomeAmount, Guid.NewGuid());

        Assert.Equal(paymentId, saga.PaymentId);
        Assert.Equal(SomeAmount, saga.PaymentAmount);
    }

    [Fact]
    public void OnPaymentProcessed_RaisesSagaStepCompletedEvent()
    {
        var saga = CreateStartedSaga();
        saga.ClearUncommittedEvents();

        saga.OnPaymentProcessed(PaymentId.New(), SomeAmount, Guid.NewGuid());

        Assert.Single(saga.UncommittedEvents);
        Assert.IsType<SagaStepCompleted>(saga.UncommittedEvents[0]);
    }

    [Fact]
    public void OnPaymentProcessed_WhenNotInPaymentPendingStep_ThrowsInvalidSagaTransitionException()
    {
        var saga = CreateStartedSaga();
        saga.OnPaymentProcessed(PaymentId.New(), SomeAmount, Guid.NewGuid());
        saga.ClearUncommittedEvents();

        Assert.Throws<InvalidSagaTransitionException>(() =>
            saga.OnPaymentProcessed(PaymentId.New(), SomeAmount, Guid.NewGuid()));
    }

    [Fact]
    public void OnPaymentFailed_FromPaymentPending_SetsStatusCompensated()
    {
        var saga = CreateStartedSaga();
        saga.ClearUncommittedEvents();

        saga.OnPaymentFailed("Card declined", Guid.NewGuid());

        Assert.Equal(SagaStatus.Compensated, saga.Status);
        Assert.Equal(SagaStep.Done, saga.CurrentStep);
    }

    [Fact]
    public void OnPaymentFailed_RaisesSagaCompensatedEvent()
    {
        var saga = CreateStartedSaga();
        saga.ClearUncommittedEvents();

        saga.OnPaymentFailed("Card declined", Guid.NewGuid());

        Assert.Single(saga.UncommittedEvents);
        Assert.IsType<SagaCompensated>(saga.UncommittedEvents[0]);
    }

    [Fact]
    public void OnStockReserved_FromInventoryPending_AdvancesToShippingPending()
    {
        var saga = CreateSagaAtInventoryPending();
        saga.ClearUncommittedEvents();

        saga.OnStockReserved(ReservationId.New(), Guid.NewGuid());

        Assert.Equal(SagaStep.ShippingPending, saga.CurrentStep);
    }

    [Fact]
    public void OnStockReserved_StoresReservationId()
    {
        var saga = CreateSagaAtInventoryPending();
        var reservationId = ReservationId.New();

        saga.OnStockReserved(reservationId, Guid.NewGuid());

        Assert.Equal(reservationId, saga.ReservationId);
    }

    [Fact]
    public void OnOutOfStock_FromInventoryPending_SetsStatusCompensatingAndStepPaymentCompensation()
    {
        var saga = CreateSagaAtInventoryPending();
        saga.ClearUncommittedEvents();

        saga.OnOutOfStock("Product X unavailable", Guid.NewGuid());

        Assert.Equal(SagaStatus.Compensating, saga.Status);
        Assert.Equal(SagaStep.PaymentCompensation, saga.CurrentStep);
    }

    [Fact]
    public void OnPaymentRefunded_FromPaymentCompensation_SetsStatusCompensated()
    {
        var saga = CreateSagaAtInventoryPending();
        saga.OnOutOfStock("No stock", Guid.NewGuid());
        saga.ClearUncommittedEvents();

        saga.OnPaymentRefunded(Guid.NewGuid());

        Assert.Equal(SagaStatus.Compensated, saga.Status);
        Assert.Equal(SagaStep.Done, saga.CurrentStep);
    }

    [Fact]
    public void OnPaymentRefunded_WhenNotInPaymentCompensationStep_ThrowsInvalidSagaTransitionException()
    {
        var saga = CreateStartedSaga();

        Assert.Throws<InvalidSagaTransitionException>(() =>
            saga.OnPaymentRefunded(Guid.NewGuid()));
    }

    [Fact]
    public void OnShipmentCreated_FromShippingPending_AdvancesToNotificationPending()
    {
        var saga = CreateSagaAtShippingPending();
        saga.ClearUncommittedEvents();

        saga.OnShipmentCreated(Guid.NewGuid());

        Assert.Equal(SagaStep.NotificationPending, saga.CurrentStep);
    }

    [Fact]
    public void OnShipmentFailed_FromShippingPending_SetsStatusCompensatingAndStepInventoryCompensation()
    {
        var saga = CreateSagaAtShippingPending();
        saga.ClearUncommittedEvents();

        saga.OnShipmentFailed("Carrier rejected", Guid.NewGuid());

        Assert.Equal(SagaStatus.Compensating, saga.Status);
        Assert.Equal(SagaStep.InventoryCompensation, saga.CurrentStep);
    }

    [Fact]
    public void OnStockReleased_FromInventoryCompensation_AdvancesToPaymentCompensation()
    {
        var saga = CreateSagaAtShippingPending();
        saga.OnShipmentFailed("Carrier rejected", Guid.NewGuid());
        saga.ClearUncommittedEvents();

        saga.OnStockReleased(Guid.NewGuid());

        Assert.Equal(SagaStep.PaymentCompensation, saga.CurrentStep);
        Assert.Equal(SagaStatus.Compensating, saga.Status);
    }

    [Fact]
    public void OnStockReleased_WhenNotInInventoryCompensationStep_ThrowsInvalidSagaTransitionException()
    {
        var saga = CreateStartedSaga();

        Assert.Throws<InvalidSagaTransitionException>(() =>
            saga.OnStockReleased(Guid.NewGuid()));
    }

    [Fact]
    public void OnNotificationSent_FromNotificationPending_SetsStatusCompleted()
    {
        var saga = CreateSagaAtNotificationPending();
        saga.ClearUncommittedEvents();

        saga.OnNotificationSent(Guid.NewGuid());

        Assert.Equal(SagaStatus.Completed, saga.Status);
        Assert.Equal(SagaStep.Done, saga.CurrentStep);
    }

    [Fact]
    public void OnNotificationSent_RaisesSagaCompletedEvent()
    {
        var saga = CreateSagaAtNotificationPending();
        saga.ClearUncommittedEvents();

        saga.OnNotificationSent(Guid.NewGuid());

        Assert.Single(saga.UncommittedEvents);
        Assert.IsType<SagaCompleted>(saga.UncommittedEvents[0]);
    }

    [Fact]
    public void ShipmentFailed_FullCompensationPath_EndsAsCompensated()
    {
        var saga = CreateSagaAtShippingPending();

        saga.OnShipmentFailed("FedEx error", Guid.NewGuid());
        saga.OnStockReleased(Guid.NewGuid());
        saga.OnPaymentRefunded(Guid.NewGuid());

        Assert.Equal(SagaStatus.Compensated, saga.Status);
        Assert.Equal(SagaStep.Done, saga.CurrentStep);
    }

    [Fact]
    public void ClearUncommittedEvents_RemovesAllEvents()
    {
        var saga = CreateStartedSaga();

        saga.ClearUncommittedEvents();

        Assert.Empty(saga.UncommittedEvents);
    }

    // Builder helpers to advance saga to a specific step without raising uncommitted events.

    private static OrderProcessingSaga CreateStartedSaga()
    {
        var saga = OrderProcessingSaga.Start(SomeOrderId, SomeCustomer, SomeAmount, SomeAddress, OneItem, Guid.NewGuid());
        return saga;
    }

    private static OrderProcessingSaga CreateSagaAtInventoryPending()
    {
        var saga = CreateStartedSaga();
        saga.OnPaymentProcessed(PaymentId.New(), SomeAmount, Guid.NewGuid());
        saga.ClearUncommittedEvents();
        return saga;
    }

    private static OrderProcessingSaga CreateSagaAtShippingPending()
    {
        var saga = CreateSagaAtInventoryPending();
        saga.OnStockReserved(ReservationId.New(), Guid.NewGuid());
        saga.ClearUncommittedEvents();
        return saga;
    }

    private static OrderProcessingSaga CreateSagaAtNotificationPending()
    {
        var saga = CreateSagaAtShippingPending();
        saga.OnShipmentCreated(Guid.NewGuid());
        saga.ClearUncommittedEvents();
        return saga;
    }
}
