using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Events.Inventory;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Application.Services;
using SagaOrchestrator.Domain.Repositories;

namespace SagaOrchestrator.Application.EventHandlers;

public sealed class StockReservedHandler : SagaEventHandlerBase<StockReserved>
{
    public StockReservedHandler(
        ISagaRepository repository,
        ISagaCommandPublisher commandPublisher,
        ILogger<StockReservedHandler> logger)
        : base(repository, commandPublisher, logger) { }

    protected override async Task HandleAsync(StockReserved evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        saga.OnStockReserved(evt.ReservationId, evt.CorrelationId);
        await Repository.SaveAsync(saga, cancellationToken);

        var shipmentItems = saga.Items
            .Select(i => new ShipmentItem(i.ProductId, i.Quantity, i.ProductId.Value.ToString()))
            .ToList()
            .AsReadOnly();

        await CommandPublisher.PublishCreateShipmentAsync(
            new CreateShipmentCommand(evt.OrderId, saga.CustomerId, saga.ShippingAddress, shipmentItems, evt.CorrelationId),
            cancellationToken);

        Logger.LogInformation(
            "Saga {SagaId}: stock reserved. Issued CreateShipment command. CorrelationId={CorrelationId}",
            saga.Id, evt.CorrelationId);
    }
}
