using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Notification;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class NotificationDeliveredHandler : IAnalyticsEventHandler
{
    private readonly INotificationMetricRepository _notificationMetricRepository;
    private readonly ILogger<NotificationDeliveredHandler> _logger;

    public string EventTypeName => nameof(NotificationDelivered);
    public Type EventType => typeof(NotificationDelivered);

    public NotificationDeliveredHandler(
        INotificationMetricRepository notificationMetricRepository,
        ILogger<NotificationDeliveredHandler> logger)
    {
        _notificationMetricRepository = notificationMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((NotificationDelivered)evt, cancellationToken);

    private async Task HandleAsync(NotificationDelivered @event, CancellationToken cancellationToken)
    {
        var notificationId = @event.NotificationId.Value;
        var metric = await _notificationMetricRepository.GetByNotificationIdAsync(notificationId, cancellationToken);

        if (metric is null)
        {
            _logger.LogWarning("NotificationDelivered received for unknown notification {NotificationId}", notificationId);
            return;
        }

        metric.Status = "Delivered";
        metric.DeliveredAt = @event.DeliveredAt.UtcDateTime;
        await _notificationMetricRepository.UpdateAsync(metric, cancellationToken);
        await _notificationMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("NotificationDelivered projection updated for notification {NotificationId}", notificationId);
    }
}
