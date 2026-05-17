using ECommerceOrderProcessing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceOrderProcessing.Infrastructure.Snapshots;

public sealed class EfCoreSnapshotStore : ISnapshotStore
{
    private readonly DbContextBase _db;

    public EfCoreSnapshotStore(DbContextBase db)
    {
        _db = db;
    }

    public Task SaveAsync(Guid aggregateId, string aggregateType, string snapshotData, int version, CancellationToken cancellationToken = default)
    {
        _db.Snapshots.Add(new AggregateSnapshot
        {
            AggregateId = aggregateId,
            AggregateType = aggregateType,
            SnapshotData = snapshotData,
            Version = version,
            CreatedAt = DateTimeOffset.UtcNow
        });
        return Task.CompletedTask;
    }

    public Task<AggregateSnapshot?> GetLatestAsync(Guid aggregateId, CancellationToken cancellationToken = default)
        => _db.Snapshots
            .Where(s => s.AggregateId == aggregateId)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync(cancellationToken);
}
