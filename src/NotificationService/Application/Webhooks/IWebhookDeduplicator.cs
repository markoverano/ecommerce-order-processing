namespace NotificationService.Application.Webhooks;

public interface IWebhookDeduplicator
{
    Task<bool> IsProcessedAsync(string webhookId, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(string webhookId, string eventType, CancellationToken cancellationToken = default);
}
