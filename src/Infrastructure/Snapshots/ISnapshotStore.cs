namespace ECommerceOrderProcessing.Infrastructure.Snapshots;

/// <summary>
/// Stores periodic aggregate state snapshots so that rehydration only needs to replay
/// events recorded after the snapshot rather than the full event stream.
/// </summary>
public interface ISnapshotStore
{
    Task<AggregateSnapshot?> GetLatestAsync(Guid aggregateId, CancellationToken cancellationToken = default);
    Task SaveAsync(AggregateSnapshot snapshot, CancellationToken cancellationToken = default);
}
