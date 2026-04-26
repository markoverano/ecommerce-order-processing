namespace ECommerceOrderProcessing.Shared.Domain;

/// <summary>Base for all domain events emitted by aggregates.</summary>
public abstract record DomainEvent
{
    public Guid AggregateId { get; init; }
    public int Version { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public Guid CorrelationId { get; init; }

    protected DomainEvent(Guid aggregateId, int version, Guid correlationId)
    {
        AggregateId = aggregateId;
        Version = version;
        Timestamp = DateTimeOffset.UtcNow;
        CorrelationId = correlationId;
    }
}
