using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Notification;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class NotificationSentHandler : IAnalyticsEventHandler
{
    private readonly INotificationMetricRepository _notificationMetricRepository;
    private readonly ILogger<NotificationSentHandler> _logger;

    public string EventTypeName => nameof(NotificationSent);
    public Type EventType => typeof(NotificationSent);

    public NotificationSentHandler(
        INotificationMetricRepository notificationMetricRepository,
        ILogger<NotificationSentHandler> logger)
    {
        _notificationMetricRepository = notificationMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((NotificationSent)evt, cancellationToken);

    private async Task HandleAsync(NotificationSent @event, CancellationToken cancellationToken)
    {
        var notificationId = @event.NotificationId.Value;

        var metric = NotificationMetric.Create(notificationId, @event.NotificationType, "Sent");
        metric.SentAt = @event.Timestamp.UtcDateTime;
        await _notificationMetricRepository.AddAsync(metric, cancellationToken);
        await _notificationMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("NotificationSent projection written for notification {NotificationId}", notificationId);
    }
}
