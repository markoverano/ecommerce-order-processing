using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Events.Inventory;
using ECommerceOrderProcessing.Shared.Events.Notification;
using ECommerceOrderProcessing.Shared.Events.Order;
using ECommerceOrderProcessing.Shared.Events.Payment;
using ECommerceOrderProcessing.Shared.Events.Shipping;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Domain.Aggregates;
using SagaOrchestrator.Domain.Enums;
using SagaOrchestrator.Domain.Exceptions;
using SagaOrchestrator.Domain.Repositories;

namespace SagaOrchestrator.Application.Services;

public sealed class SagaOrchestrationService
{
    private readonly ISagaRepository _repository;
    private readonly ISagaCommandPublisher _commandPublisher;
    private readonly ILogger<SagaOrchestrationService> _logger;

    public SagaOrchestrationService(
        ISagaRepository repository,
        ISagaCommandPublisher commandPublisher,
        ILogger<SagaOrchestrationService> logger)
    {
        _repository = repository;
        _commandPublisher = commandPublisher;
        _logger = logger;
    }

    public async Task HandleOrderCreatedAsync(OrderCreated evt, CancellationToken cancellationToken)
    {
        var saga = OrderProcessingSaga.Start(
            evt.OrderId,
            evt.CustomerId,
            evt.TotalAmount,
            evt.ShippingAddress,
            evt.Items,
            evt.CorrelationId);

        await _repository.SaveAsync(saga, cancellationToken);

        // Payment method ID is not carried on OrderCreated; downstream payment service resolves it from customer profile.
        await _commandPublisher.PublishProcessPaymentAsync(
            new ProcessPaymentCommand(evt.OrderId, evt.CustomerId, evt.TotalAmount, string.Empty, evt.CorrelationId),
            cancellationToken);

        _logger.LogInformation(
            "Saga {SagaId} started for order {OrderId}. Issued ProcessPayment command. CorrelationId={CorrelationId}",
            saga.Id, evt.OrderId, evt.CorrelationId);
    }

    public async Task HandlePaymentProcessedAsync(PaymentProcessed evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        saga.OnPaymentProcessed(evt.PaymentId, evt.Amount, evt.CorrelationId);
        await _repository.SaveAsync(saga, cancellationToken);

        var items = saga.Items
            .Select(i => new StockReservationItem(i.ProductId, i.Quantity))
            .ToList()
            .AsReadOnly();

        await _commandPublisher.PublishReserveStockAsync(
            new ReserveStockCommand(evt.OrderId, items, evt.CorrelationId),
            cancellationToken);

        _logger.LogInformation(
            "Saga {SagaId}: payment processed. Issued ReserveStock command. CorrelationId={CorrelationId}",
            saga.Id, evt.CorrelationId);
    }

    public async Task HandlePaymentFailedAsync(PaymentFailed evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        saga.OnPaymentFailed(evt.Reason, evt.CorrelationId);
        await _repository.SaveAsync(saga, cancellationToken);

        _logger.LogWarning(
            "Saga {SagaId}: payment failed ({Reason}). Saga compensated without further actions. CorrelationId={CorrelationId}",
            saga.Id, evt.Reason, evt.CorrelationId);
    }

    public async Task HandleStockReservedAsync(StockReserved evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        saga.OnStockReserved(evt.ReservationId, evt.CorrelationId);
        await _repository.SaveAsync(saga, cancellationToken);

        var shipmentItems = saga.Items
            .Select(i => new ShipmentItem(i.ProductId, i.Quantity, i.ProductId.Value.ToString()))
            .ToList()
            .AsReadOnly();

        await _commandPublisher.PublishCreateShipmentAsync(
            new CreateShipmentCommand(evt.OrderId, saga.CustomerId, saga.ShippingAddress, shipmentItems, evt.CorrelationId),
            cancellationToken);

        _logger.LogInformation(
            "Saga {SagaId}: stock reserved. Issued CreateShipment command. CorrelationId={CorrelationId}",
            saga.Id, evt.CorrelationId);
    }

    public async Task HandleOutOfStockAsync(OutOfStock evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        var reason = $"Product {evt.ProductId} out of stock (requested {evt.RequestedQuantity}, available {evt.AvailableQuantity}).";
        saga.OnOutOfStock(reason, evt.CorrelationId);
        await _repository.SaveAsync(saga, cancellationToken);

        await _commandPublisher.PublishRefundPaymentAsync(
            new RefundPaymentCommand(
                saga.PaymentId!.Value,
                evt.OrderId,
                saga.PaymentAmount ?? throw new InvalidOperationException("PaymentAmount must be set before compensation."),
                reason,
                evt.CorrelationId),
            cancellationToken);

        _logger.LogWarning(
            "Saga {SagaId}: out of stock. Issued RefundPayment command. CorrelationId={CorrelationId}",
            saga.Id, evt.CorrelationId);
    }

    public async Task HandleShipmentCreatedAsync(ShipmentCreated evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        saga.OnShipmentCreated(evt.CorrelationId);
        await _repository.SaveAsync(saga, cancellationToken);

        var templateData = new Dictionary<string, string>
        {
            ["orderId"] = evt.OrderId.Value.ToString(),
            ["trackingNumber"] = evt.TrackingNumber
        }.AsReadOnly();

        await _commandPublisher.PublishNotifyCustomerAsync(
            new NotifyCustomerCommand(evt.OrderId, saga.CustomerId, "OrderConfirmed", templateData, evt.CorrelationId),
            cancellationToken);

        _logger.LogInformation(
            "Saga {SagaId}: shipment created (tracking {TrackingNumber}). Issued NotifyCustomer command. CorrelationId={CorrelationId}",
            saga.Id, evt.TrackingNumber, evt.CorrelationId);
    }

    public async Task HandleShipmentFailedAsync(ShipmentFailed evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        saga.OnShipmentFailed(evt.Reason, evt.CorrelationId);
        await _repository.SaveAsync(saga, cancellationToken);

        await _commandPublisher.PublishReleaseStockAsync(
            new ReleaseStockCommand(saga.ReservationId!.Value, evt.OrderId, evt.CorrelationId),
            cancellationToken);

        _logger.LogWarning(
            "Saga {SagaId}: shipment failed. Issued ReleaseStock command to begin compensation. CorrelationId={CorrelationId}",
            saga.Id, evt.CorrelationId);
    }

    public async Task HandleStockReleasedAsync(StockReleased evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        // StockReleased during compensation (ShipmentFailed path) — refund payment next.
        if (saga.Status != SagaStatus.Compensating || saga.CurrentStep != SagaStep.InventoryCompensation)
        {
            _logger.LogDebug(
                "Saga {SagaId}: ignoring StockReleased — not in InventoryCompensation step (Status={Status}, Step={Step}). CorrelationId={CorrelationId}",
                saga.Id, saga.Status, saga.CurrentStep, evt.CorrelationId);
            return;
        }

        saga.OnStockReleased(evt.CorrelationId);
        await _repository.SaveAsync(saga, cancellationToken);

        await _commandPublisher.PublishRefundPaymentAsync(
            new RefundPaymentCommand(
                saga.PaymentId!.Value,
                evt.OrderId,
                saga.PaymentAmount ?? throw new InvalidOperationException("PaymentAmount must be set before compensation."),
                saga.CompensationReason ?? "Shipment failed.",
                evt.CorrelationId),
            cancellationToken);

        _logger.LogInformation(
            "Saga {SagaId}: stock released. Issued RefundPayment command. CorrelationId={CorrelationId}",
            saga.Id, evt.CorrelationId);
    }

    public async Task HandlePaymentRefundedAsync(PaymentRefunded evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        saga.OnPaymentRefunded(evt.CorrelationId);
        await _repository.SaveAsync(saga, cancellationToken);

        _logger.LogInformation(
            "Saga {SagaId}: payment refunded. Saga fully compensated. CorrelationId={CorrelationId}",
            saga.Id, evt.CorrelationId);
    }

    public async Task HandleNotificationSentAsync(NotificationSent evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        saga.OnNotificationSent(evt.CorrelationId);
        await _repository.SaveAsync(saga, cancellationToken);

        _logger.LogInformation(
            "Saga {SagaId}: notification sent. Saga completed successfully. CorrelationId={CorrelationId}",
            saga.Id, evt.CorrelationId);
    }

    private async Task<OrderProcessingSaga> LoadSagaOrThrowAsync(OrderId orderId, CancellationToken cancellationToken)
    {
        return await _repository.GetByOrderIdAsync(orderId, cancellationToken)
            ?? throw new SagaNotFoundException(orderId.Value);
    }
}
