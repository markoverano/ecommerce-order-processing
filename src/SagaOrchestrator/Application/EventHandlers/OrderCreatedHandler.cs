using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Events.Order;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Application.Metrics;
using SagaOrchestrator.Application.Services;
using SagaOrchestrator.Domain.Aggregates;
using SagaOrchestrator.Domain.Repositories;

namespace SagaOrchestrator.Application.EventHandlers;

public sealed class OrderCreatedHandler : SagaEventHandlerBase<OrderCreated>
{
    public OrderCreatedHandler(
        ISagaRepository repository,
        ISagaCommandPublisher commandPublisher,
        ILogger<OrderCreatedHandler> logger)
        : base(repository, commandPublisher, logger) { }

    protected override async Task HandleAsync(OrderCreated evt, CancellationToken cancellationToken)
    {
        var saga = OrderProcessingSaga.Start(
            evt.OrderId,
            evt.CustomerId,
            evt.TotalAmount,
            evt.ShippingAddress,
            evt.Items,
            evt.CorrelationId);

        await Repository.SaveAsync(saga, cancellationToken);

        SagaMetrics.SagasStarted.Inc();

        // Payment method ID is not carried on OrderCreated; downstream payment service resolves it from customer profile.
        await CommandPublisher.PublishProcessPaymentAsync(
            new ProcessPaymentCommand(evt.OrderId, evt.CustomerId, evt.TotalAmount, string.Empty, evt.CorrelationId),
            cancellationToken);

        Logger.LogInformation(
            "Saga {SagaId} started for order {OrderId}. Issued ProcessPayment command. CorrelationId={CorrelationId}",
            saga.Id, evt.OrderId, evt.CorrelationId);
    }
}
