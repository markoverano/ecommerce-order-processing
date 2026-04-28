namespace ECommerceOrderProcessing.Infrastructure.Persistence;

public sealed class StoredEvent
{
    public long EventId { get; init; }
    public Guid AggregateId { get; init; }
    public string AggregateType { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string EventData { get; init; } = string.Empty;
    public int Version { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public Guid CorrelationId { get; init; }
}
