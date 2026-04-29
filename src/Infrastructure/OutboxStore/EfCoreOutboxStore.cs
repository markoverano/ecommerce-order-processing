using ECommerceOrderProcessing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceOrderProcessing.Infrastructure.OutboxStore;

public sealed class EfCoreOutboxStore : IOutboxStore
{
    private readonly DbContextBase _db;

    public EfCoreOutboxStore(DbContextBase db) => _db = db;

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _db.OutboxMessages.Add(message);
        // Caller is responsible for calling SaveChangesAsync in the same transaction as the aggregate write.
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnpublishedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        return await _db.OutboxMessages
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkPublishedAsync(long id, CancellationToken cancellationToken = default)
    {
        var message = await _db.OutboxMessages.FindAsync(new object[] { id }, cancellationToken)
            ?? throw new InvalidOperationException($"Outbox message {id} not found.");
        message.MarkPublished(DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
