namespace ECommerceOrderProcessing.Infrastructure.OutboxStore;

public sealed class OutboxMessage
{
    public long Id { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string EventData { get; init; } = string.Empty;
    public string RoutingKey { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public bool IsPublished => PublishedAt.HasValue;

    public void MarkPublished(DateTimeOffset at) => PublishedAt = at;

    public static OutboxMessage Create(string eventType, string eventData, string routingKey) =>
        new()
        {
            EventType = eventType,
            EventData = eventData,
            RoutingKey = routingKey,
            CreatedAt = DateTimeOffset.UtcNow
        };
}
