using ECommerceOrderProcessing.Shared.Domain;

namespace ECommerceOrderProcessing.Infrastructure.EventStore;

/// <summary>Append-only log of domain events per aggregate.</summary>
public interface IEventStore
{
    Task AppendEventsAsync(Guid aggregateId, string aggregateType, IReadOnlyList<DomainEvent> events, int expectedVersion, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DomainEvent>> GetEventsAsync(Guid aggregateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DomainEvent>> GetEventsSinceAsync(Guid aggregateId, int fromVersion, CancellationToken cancellationToken = default);
}
