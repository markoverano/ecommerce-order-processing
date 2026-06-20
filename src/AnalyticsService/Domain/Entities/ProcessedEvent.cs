namespace AnalyticsService.Domain.Entities;

public record ProcessedEvent
{
    public long Id { get; init; }
    public required string EventId { get; init; }
    public required string EventType { get; set; }
    public DateTime ProcessedAt { get; init; }
    public required string CorrelationId { get; set; }

    public static ProcessedEvent Create(string eventId, string eventType, string correlationId)
    {
        return new ProcessedEvent
        {
            EventId = eventId,
            EventType = eventType,
            ProcessedAt = DateTime.UtcNow,
            CorrelationId = correlationId
        };
    }
}
