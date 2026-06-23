using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Saga;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using SagaOrchestrator.Domain.Enums;
using SagaOrchestrator.Domain.Exceptions;

namespace SagaOrchestrator.Domain.Aggregates;

file static class SagaTransitions
{
    public static (SagaStatus?, SagaStep?) GetNextState(SagaStatus status, string step)
    {
        if (status == SagaStatus.Running)
        {
            return step switch
            {
                nameof(SagaStep.PaymentPending) => (SagaStatus.Running, SagaStep.InventoryPending),
                nameof(SagaStep.InventoryPending) => (SagaStatus.Running, SagaStep.ShippingPending),
                "InventoryPending.OutOfStock" => (SagaStatus.Compensating, SagaStep.PaymentCompensation),
                nameof(SagaStep.ShippingPending) => (SagaStatus.Running, SagaStep.NotificationPending),
                "ShippingPending.Failed" => (SagaStatus.Compensating, SagaStep.InventoryCompensation),
                _ => (null, null)
            };
        }

        if (status == SagaStatus.Compensating)
        {
            return step switch
            {
                nameof(SagaStep.InventoryCompensation) => (SagaStatus.Compensating, SagaStep.PaymentCompensation),
                _ => (null, null)
            };
        }

        return (null, null);
    }
}

public sealed class OrderProcessingSaga : AggregateRoot
{
    public OrderId OrderId { get; private set; }
    public CustomerId CustomerId { get; private set; }
    public SagaStatus Status { get; private set; }
    public SagaStep CurrentStep { get; private set; }
    public Money TotalAmount { get; private set; }
    public ShippingAddress ShippingAddress { get; private set; }

    private readonly List<OrderItemData> _items = new();
    public IReadOnlyList<OrderItemData> Items => _items.AsReadOnly();

    // Accumulated during forward steps for use in compensation commands.
    public PaymentId? PaymentId { get; private set; }
    public Money? PaymentAmount { get; private set; }
    public ReservationId? ReservationId { get; private set; }
    public string? CompensationReason { get; private set; }

    private OrderProcessingSaga() { }

    public static OrderProcessingSaga Start(
        OrderId orderId,
        CustomerId customerId,
        Money totalAmount,
        ShippingAddress shippingAddress,
        IReadOnlyList<OrderItemData> items,
        Guid correlationId)
    {
        var saga = new OrderProcessingSaga();
        var sagaId = Guid.NewGuid();

        saga._items.AddRange(items);
        saga.TotalAmount = totalAmount;
        saga.ShippingAddress = shippingAddress;
        saga.CustomerId = customerId;

        saga.RaiseEvent(new SagaStarted(sagaId, orderId, correlationId));
        return saga;
    }

    public void OnPaymentProcessed(PaymentId paymentId, Money amount, Guid correlationId)
    {
        if (Status != SagaStatus.Running || CurrentStep != SagaStep.PaymentPending)
            throw new InvalidSagaTransitionException(
                $"Cannot apply PaymentProcessed in state {Status}/{CurrentStep}.");

        PaymentId = paymentId;
        PaymentAmount = amount;
        RaiseEvent(new SagaStepCompleted(Id, OrderId, nameof(SagaStep.PaymentPending), Version + 1, correlationId));
    }

    public void OnPaymentFailed(string reason, Guid correlationId)
    {
        if (Status != SagaStatus.Running || CurrentStep != SagaStep.PaymentPending)
            throw new InvalidSagaTransitionException(
                $"Cannot apply PaymentFailed in state {Status}/{CurrentStep}.");

        CompensationReason = reason;
        RaiseEvent(new SagaCompensated(Id, OrderId, reason, Version + 1, correlationId));
    }

    public void OnStockReserved(ReservationId reservationId, Guid correlationId)
    {
        if (Status != SagaStatus.Running || CurrentStep != SagaStep.InventoryPending)
            throw new InvalidSagaTransitionException(
                $"Cannot apply StockReserved in state {Status}/{CurrentStep}.");

        ReservationId = reservationId;
        RaiseEvent(new SagaStepCompleted(Id, OrderId, nameof(SagaStep.InventoryPending), Version + 1, correlationId));
    }

    public void OnOutOfStock(string reason, Guid correlationId)
    {
        if (Status != SagaStatus.Running || CurrentStep != SagaStep.InventoryPending)
            throw new InvalidSagaTransitionException(
                $"Cannot apply OutOfStock in state {Status}/{CurrentStep}.");

        CompensationReason = reason;
        RaiseEvent(new SagaStepCompleted(Id, OrderId, nameof(SagaStep.InventoryPending) + ".OutOfStock", Version + 1, correlationId));
    }

    public void OnShipmentCreated(Guid correlationId)
    {
        if (Status != SagaStatus.Running || CurrentStep != SagaStep.ShippingPending)
            throw new InvalidSagaTransitionException(
                $"Cannot apply ShipmentCreated in state {Status}/{CurrentStep}.");

        RaiseEvent(new SagaStepCompleted(Id, OrderId, nameof(SagaStep.ShippingPending), Version + 1, correlationId));
    }

    public void OnShipmentFailed(string reason, Guid correlationId)
    {
        if (Status != SagaStatus.Running || CurrentStep != SagaStep.ShippingPending)
            throw new InvalidSagaTransitionException(
                $"Cannot apply ShipmentFailed in state {Status}/{CurrentStep}.");

        CompensationReason = reason;
        RaiseEvent(new SagaStepCompleted(Id, OrderId, nameof(SagaStep.ShippingPending) + ".Failed", Version + 1, correlationId));
    }

    public void OnStockReleased(Guid correlationId)
    {
        if (Status != SagaStatus.Compensating || CurrentStep != SagaStep.InventoryCompensation)
            throw new InvalidSagaTransitionException(
                $"Cannot apply StockReleased in state {Status}/{CurrentStep}.");

        RaiseEvent(new SagaStepCompleted(Id, OrderId, nameof(SagaStep.InventoryCompensation), Version + 1, correlationId));
    }

    public void OnPaymentRefunded(Guid correlationId)
    {
        if (Status != SagaStatus.Compensating || CurrentStep != SagaStep.PaymentCompensation)
            throw new InvalidSagaTransitionException(
                $"Cannot apply PaymentRefunded in state {Status}/{CurrentStep}.");

        RaiseEvent(new SagaCompensated(Id, OrderId, CompensationReason ?? "Compensation complete.", Version + 1, correlationId));
    }

    public void OnNotificationSent(Guid correlationId)
    {
        if (Status != SagaStatus.Running || CurrentStep != SagaStep.NotificationPending)
            throw new InvalidSagaTransitionException(
                $"Cannot apply NotificationSent in state {Status}/{CurrentStep}.");

        RaiseEvent(new SagaCompleted(Id, OrderId, Version + 1, correlationId));
    }

    // Reconstructs an aggregate from a persisted snapshot without raising new uncommitted events.
    internal static OrderProcessingSaga FromSnapshot(
        Guid sagaId,
        OrderId orderId,
        CustomerId customerId,
        SagaStatus status,
        SagaStep currentStep,
        Money totalAmount,
        ShippingAddress shippingAddress,
        IReadOnlyList<OrderItemData> items,
        PaymentId? paymentId,
        Money? paymentAmount,
        ReservationId? reservationId,
        string? compensationReason,
        int version)
    {
        var saga = new OrderProcessingSaga();
        saga.Id = sagaId;
        saga.OrderId = orderId;
        saga.CustomerId = customerId;
        saga.Status = status;
        saga.CurrentStep = currentStep;
        saga.TotalAmount = totalAmount;
        saga.ShippingAddress = shippingAddress;
        saga._items.AddRange(items);
        saga.PaymentId = paymentId;
        saga.PaymentAmount = paymentAmount;
        saga.ReservationId = reservationId;
        saga.CompensationReason = compensationReason;
        saga.Version = version;
        return saga;
    }

    protected override void Apply(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case SagaStarted e:
                Id = e.SagaId;
                OrderId = e.OrderId;
                Status = SagaStatus.Running;
                CurrentStep = SagaStep.PaymentPending;
                break;

            case SagaStepCompleted e:
                var (newStatus, newStep) = SagaTransitions.GetNextState(Status, e.Step);
                if (newStatus.HasValue)
                {
                    Status = newStatus.Value;
                    CurrentStep = newStep!.Value;
                }
                break;

            case SagaCompleted:
                Status = SagaStatus.Completed;
                CurrentStep = SagaStep.Done;
                break;

            case SagaCompensated:
                Status = SagaStatus.Compensated;
                CurrentStep = SagaStep.Done;
                break;
        }
    }
}
