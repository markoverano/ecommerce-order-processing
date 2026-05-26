using ECommerceOrderProcessing.Shared.ValueObjects;
using ECommerceOrderProcessing.Shared.Webhooks;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Repositories;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Repositories;

namespace NotificationService.Application.Webhooks;

/// <summary>
/// Processes validated Mailgun webhook events. Deduplicates, routes to the Notification aggregate,
/// and persists via the write-side repository (which writes outbox in the same transaction).
/// </summary>
public sealed class MailgunWebhookHandler
{
    private readonly INotificationRepository _repository;
    private readonly INotificationReadRepository _readRepository;
    private readonly IWebhookDeduplicator _deduplicator;
    private readonly ILogger<MailgunWebhookHandler> _logger;

    public MailgunWebhookHandler(
        INotificationRepository repository,
        INotificationReadRepository readRepository,
        IWebhookDeduplicator deduplicator,
        ILogger<MailgunWebhookHandler> logger)
    {
        _repository = repository;
        _readRepository = readRepository;
        _deduplicator = deduplicator;
        _logger = logger;
    }

    public async Task HandleAsync(
        string webhookId,
        string eventType,
        string providerMessageId,
        DateTimeOffset? eventTimestamp,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (await _deduplicator.IsProcessedAsync(webhookId, cancellationToken))
        {
            _logger.LogInformation("Mailgun webhook {WebhookId} already processed, skipping.", webhookId);
            return;
        }

        switch (eventType)
        {
            case "delivered":
                await HandleDeliveredAsync(providerMessageId, eventTimestamp ?? DateTimeOffset.UtcNow, correlationId, cancellationToken);
                break;
            case "failed":
            case "complained":
                await HandleFailedAsync(providerMessageId, eventType, correlationId, cancellationToken);
                break;
            default:
                _logger.LogDebug("Ignoring Mailgun webhook event type {EventType}", eventType);
                break;
        }

        await _deduplicator.MarkProcessedAsync(webhookId, eventType, cancellationToken);
    }

    private async Task HandleDeliveredAsync(string providerMessageId, DateTimeOffset deliveredAt, Guid correlationId, CancellationToken ct)
    {
        var notificationId = await _readRepository.FindByProviderMessageIdAsync(providerMessageId, ct);
        if (notificationId is null)
        {
            _logger.LogWarning("No notification found for Mailgun message id {MessageId}", providerMessageId);
            return;
        }

        var notification = await _repository.GetByIdAsync(notificationId.Value, ct);
        if (notification is null || notification.Status != NotificationStatus.Sent)
            return;

        notification.MarkAsDelivered(deliveredAt, correlationId);
        await _repository.SaveAsync(notification, ct);

        _logger.LogInformation("Notification {NotificationId} confirmed as delivered via Mailgun webhook.", notificationId);
    }

    private async Task HandleFailedAsync(string providerMessageId, string reason, Guid correlationId, CancellationToken ct)
    {
        var notificationId = await _readRepository.FindByProviderMessageIdAsync(providerMessageId, ct);
        if (notificationId is null)
        {
            _logger.LogWarning("No notification found for Mailgun message id {MessageId}", providerMessageId);
            return;
        }

        var notification = await _repository.GetByIdAsync(notificationId.Value, ct);
        if (notification is null || notification.Status == NotificationStatus.Delivered)
            return;

        notification.MarkAsFailed(reason, correlationId);
        await _repository.SaveAsync(notification, ct);

        _logger.LogInformation("Notification {NotificationId} marked as failed via Mailgun webhook.", notificationId);
    }
}
