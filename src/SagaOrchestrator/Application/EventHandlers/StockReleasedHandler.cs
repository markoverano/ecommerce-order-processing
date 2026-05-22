using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Events.Inventory;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Application.Services;
using SagaOrchestrator.Domain.Enums;
using SagaOrchestrator.Domain.Repositories;

namespace SagaOrchestrator.Application.EventHandlers;

public sealed class StockReleasedHandler : SagaEventHandlerBase<StockReleased>
{
    public StockReleasedHandler(
        ISagaRepository repository,
        ISagaCommandPublisher commandPublisher,
        ILogger<StockReleasedHandler> logger)
        : base(repository, commandPublisher, logger) { }

    protected override async Task HandleAsync(StockReleased evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        // StockReleased during compensation (ShipmentFailed path) — refund payment next.
        if (saga.Status != SagaStatus.Compensating || saga.CurrentStep != SagaStep.InventoryCompensation)
        {
            Logger.LogDebug(
                "Saga {SagaId}: ignoring StockReleased — not in InventoryCompensation step (Status={Status}, Step={Step}). CorrelationId={CorrelationId}",
                saga.Id, saga.Status, saga.CurrentStep, evt.CorrelationId);
            return;
        }

        saga.OnStockReleased(evt.CorrelationId);
        await Repository.SaveAsync(saga, cancellationToken);

        await CommandPublisher.PublishRefundPaymentAsync(
            new RefundPaymentCommand(
                saga.PaymentId!.Value,
                evt.OrderId,
                saga.PaymentAmount ?? throw new InvalidOperationException("PaymentAmount must be set before compensation."),
                saga.CompensationReason ?? "Shipment failed.",
                evt.CorrelationId),
            cancellationToken);

        Logger.LogInformation(
            "Saga {SagaId}: stock released. Issued RefundPayment command. CorrelationId={CorrelationId}",
            saga.Id, evt.CorrelationId);
    }
}
