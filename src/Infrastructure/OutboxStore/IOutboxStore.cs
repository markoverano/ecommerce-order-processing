namespace ECommerceOrderProcessing.Infrastructure.OutboxStore;

/// <summary>Transactional outbox that decouples aggregate writes from message publishing.</summary>
public interface IOutboxStore
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> GetUnpublishedAsync(int batchSize, CancellationToken cancellationToken = default);
    Task MarkPublishedAsync(long id, CancellationToken cancellationToken = default);
}
