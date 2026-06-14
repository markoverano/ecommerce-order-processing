using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Events.Inventory;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Application.Services;
using SagaOrchestrator.Domain.Repositories;

namespace SagaOrchestrator.Application.EventHandlers;

public sealed class OutOfStockHandler : SagaEventHandlerBase<OutOfStock>
{
    public OutOfStockHandler(
        ISagaRepository repository,
        ISagaCommandPublisher commandPublisher,
        ILogger<OutOfStockHandler> logger)
        : base(repository, commandPublisher, logger) { }

    protected override async Task HandleAsync(OutOfStock evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, evt, cancellationToken);

        var reason = $"Product {evt.ProductId} out of stock (requested {evt.RequestedQuantity}, available {evt.AvailableQuantity}).";
        saga.OnOutOfStock(reason, evt.CorrelationId);
        await Repository.SaveAsync(saga, cancellationToken);

        await CommandPublisher.PublishRefundPaymentAsync(
            new RefundPaymentCommand(
                saga.PaymentId!.Value,
                evt.OrderId,
                saga.PaymentAmount ?? throw new InvalidOperationException("PaymentAmount must be set before compensation."),
                reason,
                evt.CorrelationId),
            cancellationToken);

        Logger.LogWarning(
            "Saga {SagaId}: out of stock. Issued RefundPayment command. CorrelationId={CorrelationId}",
            saga.Id, evt.CorrelationId);
    }
}
