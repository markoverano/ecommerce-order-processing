namespace ECommerceOrderProcessing.Shared.Webhooks;

/// <summary>Prevents duplicate processing of webhook deliveries by tracking processed webhook IDs.</summary>
public interface IWebhookDeduplicator
{
    Task<bool> IsProcessedAsync(string webhookId, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(string webhookId, string eventType, CancellationToken cancellationToken = default);
}
