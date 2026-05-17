namespace ECommerceOrderProcessing.Infrastructure.Snapshots;

/// <summary>
/// Stores and retrieves aggregate state snapshots.
/// Snapshots allow rehydration from the last checkpoint rather than replaying the entire event stream.
/// </summary>
public interface ISnapshotStore
{
    Task SaveAsync(Guid aggregateId, string aggregateType, string snapshotData, int version, CancellationToken cancellationToken = default);
    Task<AggregateSnapshot?> GetLatestAsync(Guid aggregateId, CancellationToken cancellationToken = default);
}
