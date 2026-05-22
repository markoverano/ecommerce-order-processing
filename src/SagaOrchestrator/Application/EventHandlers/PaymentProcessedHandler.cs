using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Events.Payment;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Application.Services;
using SagaOrchestrator.Domain.Repositories;

namespace SagaOrchestrator.Application.EventHandlers;

public sealed class PaymentProcessedHandler : SagaEventHandlerBase<PaymentProcessed>
{
    public PaymentProcessedHandler(
        ISagaRepository repository,
        ISagaCommandPublisher commandPublisher,
        ILogger<PaymentProcessedHandler> logger)
        : base(repository, commandPublisher, logger) { }

    protected override async Task HandleAsync(PaymentProcessed evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        saga.OnPaymentProcessed(evt.PaymentId, evt.Amount, evt.CorrelationId);
        await Repository.SaveAsync(saga, cancellationToken);

        var items = saga.Items
            .Select(i => new StockReservationItem(i.ProductId, i.Quantity))
            .ToList()
            .AsReadOnly();

        await CommandPublisher.PublishReserveStockAsync(
            new ReserveStockCommand(evt.OrderId, items, evt.CorrelationId),
            cancellationToken);

        Logger.LogInformation(
            "Saga {SagaId}: payment processed. Issued ReserveStock command. CorrelationId={CorrelationId}",
            saga.Id, evt.CorrelationId);
    }
}
