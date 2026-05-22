using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Events.Shipping;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Application.Services;
using SagaOrchestrator.Domain.Repositories;

namespace SagaOrchestrator.Application.EventHandlers;

public sealed class ShipmentCreatedHandler : SagaEventHandlerBase<ShipmentCreated>
{
    public ShipmentCreatedHandler(
        ISagaRepository repository,
        ISagaCommandPublisher commandPublisher,
        ILogger<ShipmentCreatedHandler> logger)
        : base(repository, commandPublisher, logger) { }

    protected override async Task HandleAsync(ShipmentCreated evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        saga.OnShipmentCreated(evt.CorrelationId);
        await Repository.SaveAsync(saga, cancellationToken);

        var templateData = new Dictionary<string, string>
        {
            ["orderId"] = evt.OrderId.Value.ToString(),
            ["trackingNumber"] = evt.TrackingNumber
        }.AsReadOnly();

        await CommandPublisher.PublishNotifyCustomerAsync(
            new NotifyCustomerCommand(evt.OrderId, saga.CustomerId, "OrderConfirmed", templateData, evt.CorrelationId),
            cancellationToken);

        Logger.LogInformation(
            "Saga {SagaId}: shipment created (tracking {TrackingNumber}). Issued NotifyCustomer command. CorrelationId={CorrelationId}",
            saga.Id, evt.TrackingNumber, evt.CorrelationId);
    }
}
