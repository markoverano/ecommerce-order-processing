namespace ShippingService.Infrastructure.Persistence;

public sealed class ProcessedWebhook
{
    public long Id { get; set; }
    public string WebhookId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}
