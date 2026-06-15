using ECommerceOrderProcessing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceOrderProcessing.Infrastructure.Snapshots;

public sealed class EfCoreSnapshotStore<TContext> : ISnapshotStore where TContext : DbContextBase
{
    private readonly TContext _db;

    public EfCoreSnapshotStore(TContext db)
    {
        _db = db;
    }

    public Task<AggregateSnapshot?> GetLatestAsync(Guid aggregateId, CancellationToken cancellationToken = default) =>
        _db.Snapshots
            .Where(s => s.AggregateId == aggregateId)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SaveAsync(AggregateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _db.Snapshots.Add(snapshot);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
