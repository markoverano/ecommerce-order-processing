using ECommerceOrderProcessing.Shared.Events.Payment;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Application.Metrics;
using SagaOrchestrator.Application.Services;
using SagaOrchestrator.Domain.Repositories;

namespace SagaOrchestrator.Application.EventHandlers;

public sealed class PaymentRefundedHandler : SagaEventHandlerBase<PaymentRefunded>
{
    public PaymentRefundedHandler(
        ISagaRepository repository,
        ISagaCommandPublisher commandPublisher,
        ILogger<PaymentRefundedHandler> logger)
        : base(repository, commandPublisher, logger) { }

    protected override async Task HandleAsync(PaymentRefunded evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, cancellationToken);

        saga.OnPaymentRefunded(evt.CorrelationId);
        await Repository.SaveAsync(saga, cancellationToken);

        SagaMetrics.SagasCompensated.WithLabels(saga.CompensationReason ?? "unknown").Inc();

        Logger.LogInformation(
            "Saga {SagaId}: payment refunded. Saga fully compensated. CorrelationId={CorrelationId}",
            saga.Id, evt.CorrelationId);
    }
}
