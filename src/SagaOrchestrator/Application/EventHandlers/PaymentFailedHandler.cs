using ECommerceOrderProcessing.Shared.Events.Payment;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Application.Metrics;
using SagaOrchestrator.Application.Services;
using SagaOrchestrator.Domain.Repositories;

namespace SagaOrchestrator.Application.EventHandlers;

public sealed class PaymentFailedHandler : SagaEventHandlerBase<PaymentFailed>
{
    public PaymentFailedHandler(
        ISagaRepository repository,
        ISagaCommandPublisher commandPublisher,
        ILogger<PaymentFailedHandler> logger)
        : base(repository, commandPublisher, logger) { }

    protected override async Task HandleAsync(PaymentFailed evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, evt, cancellationToken);

        saga.OnPaymentFailed(evt.Reason, evt.CorrelationId);
        await Repository.SaveAsync(saga, cancellationToken);

        SagaMetrics.SagasCompensated.WithLabels("payment_failed").Inc();

        Logger.LogWarning(
            "Saga {SagaId}: payment failed ({Reason}). Saga compensated without further actions. CorrelationId={CorrelationId}",
            saga.Id, evt.Reason, evt.CorrelationId);
    }
}
