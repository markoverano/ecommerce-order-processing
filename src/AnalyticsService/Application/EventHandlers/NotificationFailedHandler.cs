using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Notification;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class NotificationFailedHandler : IAnalyticsEventHandler
{
    private readonly INotificationMetricRepository _notificationMetricRepository;
    private readonly ILogger<NotificationFailedHandler> _logger;

    public string EventTypeName => nameof(NotificationFailed);
    public Type EventType => typeof(NotificationFailed);

    public NotificationFailedHandler(
        INotificationMetricRepository notificationMetricRepository,
        ILogger<NotificationFailedHandler> logger)
    {
        _notificationMetricRepository = notificationMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((NotificationFailed)evt, cancellationToken);

    private async Task HandleAsync(NotificationFailed @event, CancellationToken cancellationToken)
    {
        var notificationId = @event.NotificationId.Value;
        var metric = await _notificationMetricRepository.GetByNotificationIdAsync(notificationId, cancellationToken);

        if (metric is null)
        {
            _logger.LogWarning("NotificationFailed received for unknown notification {NotificationId}", notificationId);
            return;
        }

        metric.Status = "Failed";
        metric.FailedAt = @event.Timestamp.UtcDateTime;
        await _notificationMetricRepository.UpdateAsync(metric, cancellationToken);
        await _notificationMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("NotificationFailed projection updated for notification {NotificationId}", notificationId);
    }
}
