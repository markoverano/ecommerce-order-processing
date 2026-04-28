using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.Persistence;
using ECommerceOrderProcessing.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECommerceOrderProcessing.Infrastructure.EventStore;

public sealed class EfCoreEventStore : IEventStore
{
    private readonly DbContextBase _db;
    private readonly ILogger<EfCoreEventStore> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public EfCoreEventStore(DbContextBase db, ILogger<EfCoreEventStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task AppendEventsAsync(Guid aggregateId, string aggregateType, IReadOnlyList<DomainEvent> events, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var currentVersion = await _db.Events
            .Where(e => e.AggregateId == aggregateId)
            .MaxAsync(e => (int?)e.Version, cancellationToken) ?? 0;

        if (currentVersion != expectedVersion)
            throw new InvalidOperationException(
                $"Optimistic concurrency conflict for aggregate {aggregateId}: expected version {expectedVersion}, found {currentVersion}.");

        foreach (var domainEvent in events)
        {
            _db.Events.Add(new StoredEvent
            {
                AggregateId = aggregateId,
                AggregateType = aggregateType,
                EventType = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().FullName!,
                EventData = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), _jsonOptions),
                Version = domainEvent.Version,
                Timestamp = domainEvent.Timestamp,
                CorrelationId = domainEvent.CorrelationId
            });
        }

        _logger.LogDebug("Appended {Count} events for aggregate {AggregateId}", events.Count, aggregateId);
    }

    public async Task<IReadOnlyList<DomainEvent>> GetEventsAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        var stored = await _db.Events
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return stored.Select(Deserialize).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<DomainEvent>> GetEventsSinceAsync(Guid aggregateId, int fromVersion, CancellationToken cancellationToken = default)
    {
        var stored = await _db.Events
            .Where(e => e.AggregateId == aggregateId && e.Version > fromVersion)
            .OrderBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return stored.Select(Deserialize).ToList().AsReadOnly();
    }

    private static DomainEvent Deserialize(StoredEvent stored)
    {
        var type = Type.GetType(stored.EventType)
            ?? throw new InvalidOperationException($"Cannot resolve event type '{stored.EventType}'.");
        return (DomainEvent)(JsonSerializer.Deserialize(stored.EventData, type, _jsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize event of type '{stored.EventType}'."));
    }
}
