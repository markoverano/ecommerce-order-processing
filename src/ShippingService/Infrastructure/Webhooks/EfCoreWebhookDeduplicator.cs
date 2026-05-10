using Microsoft.EntityFrameworkCore;
using ShippingService.Application.Webhooks;
using ShippingService.Infrastructure.Persistence;

namespace ShippingService.Infrastructure.Webhooks;

public sealed class EfCoreWebhookDeduplicator : IWebhookDeduplicator
{
    private readonly ShippingDbContext _db;

    public EfCoreWebhookDeduplicator(ShippingDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsProcessedAsync(string webhookId, CancellationToken cancellationToken = default)
    {
        return await _db.ProcessedWebhooks
            .AsNoTracking()
            .AnyAsync(x => x.WebhookId == webhookId, cancellationToken);
    }

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
