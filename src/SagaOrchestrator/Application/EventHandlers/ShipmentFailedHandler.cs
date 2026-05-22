using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Events.Shipping;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Application.Services;
using SagaOrchestrator.Domain.Repositories;

namespace SagaOrchestrator.Application.EventHandlers;

public sealed class ShipmentFailedHandler : SagaEventHandlerBase<ShipmentFailed>
{
    public ShipmentFailedHandler(
        ISagaRepository repository,
        ISagaCommandPublisher commandPublisher,
        ILogger<ShipmentFailedHandler> logger)
        : base(repository, commandPublisher, logger) { }

    protected override async Task HandleAsync(ShipmentFailed evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        saga.OnShipmentFailed(evt.Reason, evt.CorrelationId);
        await Repository.SaveAsync(saga, cancellationToken);

        await CommandPublisher.PublishReleaseStockAsync(
            new ReleaseStockCommand(saga.ReservationId!.Value, evt.OrderId, evt.CorrelationId),
            cancellationToken);

        Logger.LogWarning(
            "Saga {SagaId}: shipment failed. Issued ReleaseStock command to begin compensation. CorrelationId={CorrelationId}",
            saga.Id, evt.CorrelationId);
    }
}
