using ECommerceOrderProcessing.Shared.Events.Notification;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Application.Metrics;
using SagaOrchestrator.Application.Services;
using SagaOrchestrator.Domain.Repositories;

namespace SagaOrchestrator.Application.EventHandlers;

public sealed class NotificationSentHandler : SagaEventHandlerBase<NotificationSent>
{
    public NotificationSentHandler(
        ISagaRepository repository,
        ISagaCommandPublisher commandPublisher,
        ILogger<NotificationSentHandler> logger)
        : base(repository, commandPublisher, logger) { }

    protected override async Task HandleAsync(NotificationSent evt, CancellationToken cancellationToken)
    {
        var saga = await LoadSagaOrThrowAsync(evt.OrderId, evt, cancellationToken);

        saga.OnNotificationSent(evt.CorrelationId);
        await Repository.SaveAsync(saga, cancellationToken);

        SagaMetrics.SagasCompleted.Inc();

        Logger.LogInformation(
            "Saga {SagaId}: notification sent. Saga completed successfully. CorrelationId={CorrelationId}",
            saga.Id, evt.CorrelationId);
    }
}
