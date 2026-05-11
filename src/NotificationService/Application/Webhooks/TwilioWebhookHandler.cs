using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Repositories;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Repositories;

namespace NotificationService.Application.Webhooks;

/// <summary>
/// Processes validated Twilio SMS status webhook events. Deduplicates, routes to the Notification
/// aggregate, and persists via the write-side repository (outbox written in the same transaction).
/// </summary>
public sealed class TwilioWebhookHandler
{
    private readonly INotificationRepository _repository;
    private readonly INotificationReadRepository _readRepository;
    private readonly IWebhookDeduplicator _deduplicator;
    private readonly ILogger<TwilioWebhookHandler> _logger;

    public TwilioWebhookHandler(
        INotificationRepository repository,
        INotificationReadRepository readRepository,
        IWebhookDeduplicator deduplicator,
        ILogger<TwilioWebhookHandler> logger)
    {
        _repository = repository;
        _readRepository = readRepository;
        _deduplicator = deduplicator;
        _logger = logger;
    }

    public async Task HandleAsync(
        string webhookId,
        string messageStatus,
        string messageSid,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (await _deduplicator.IsProcessedAsync(webhookId, cancellationToken))
        {
            _logger.LogInformation("Twilio webhook {WebhookId} already processed, skipping.", webhookId);
            return;
        }

        switch (messageStatus)
        {
            case "delivered":
                await HandleDeliveredAsync(messageSid, DateTimeOffset.UtcNow, correlationId, cancellationToken);
                break;
            case "failed":
            case "undelivered":
                await HandleFailedAsync(messageSid, messageStatus, correlationId, cancellationToken);
                break;
            default:
                _logger.LogDebug("Ignoring Twilio status callback {MessageStatus}", messageStatus);
                break;
        }

        await _deduplicator.MarkProcessedAsync(webhookId, messageStatus, cancellationToken);
    }

    private async Task HandleDeliveredAsync(string messageSid, DateTimeOffset deliveredAt, Guid correlationId, CancellationToken ct)
    {
        var notificationId = await _readRepository.FindByProviderMessageIdAsync(messageSid, ct);
        if (notificationId is null)
        {
            _logger.LogWarning("No notification found for Twilio SID {MessageSid}", messageSid);
            return;
        }

        var notification = await _repository.GetByIdAsync(notificationId.Value, ct);
        if (notification is null || notification.Status != NotificationStatus.Sent)
            return;

        notification.MarkAsDelivered(deliveredAt, correlationId);
        await _repository.SaveAsync(notification, ct);

        _logger.LogInformation("Notification {NotificationId} confirmed as delivered via Twilio webhook.", notificationId);
    }

    private async Task HandleFailedAsync(string messageSid, string reason, Guid correlationId, CancellationToken ct)
    {
        var notificationId = await _readRepository.FindByProviderMessageIdAsync(messageSid, ct);
        if (notificationId is null)
        {
            _logger.LogWarning("No notification found for Twilio SID {MessageSid}", messageSid);
            return;
        }

        var notification = await _repository.GetByIdAsync(notificationId.Value, ct);
        if (notification is null || notification.Status == NotificationStatus.Delivered)
            return;

        notification.MarkAsFailed(reason, correlationId);
        await _repository.SaveAsync(notification, ct);

        _logger.LogInformation("Notification {NotificationId} marked as failed via Twilio webhook.", notificationId);
    }
}
