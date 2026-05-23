namespace ECommerceOrderProcessing.Infrastructure.Persistence;

public sealed class ProcessedWebhook
{
    public long Id { get; init; }
    public string WebhookId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; init; }
}
