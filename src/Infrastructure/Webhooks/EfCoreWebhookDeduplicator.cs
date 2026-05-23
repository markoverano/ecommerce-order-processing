using ECommerceOrderProcessing.Infrastructure.Persistence;
using ECommerceOrderProcessing.Shared.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace ECommerceOrderProcessing.Infrastructure.Webhooks;

public sealed class EfCoreWebhookDeduplicator<TContext> : IWebhookDeduplicator
    where TContext : DbContextBase
{
    private readonly TContext _db;

    public EfCoreWebhookDeduplicator(TContext db)
    {
        _db = db;
    }

    public async Task<bool> IsProcessedAsync(string webhookId, CancellationToken cancellationToken = default) =>
        await _db.ProcessedWebhooks.AsNoTracking().AnyAsync(x => x.WebhookId == webhookId, cancellationToken);

    public async Task MarkProcessedAsync(string webhookId, string eventType, CancellationToken cancellationToken = default)
    {
        _db.ProcessedWebhooks.Add(new ProcessedWebhook
        {
            WebhookId = webhookId,
            EventType = eventType,
            ProcessedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
