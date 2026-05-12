using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Webhooks;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.Webhooks;

public sealed class EfCoreWebhookDeduplicator : IWebhookDeduplicator
{
    private readonly NotificationDbContext _db;

    public EfCoreWebhookDeduplicator(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsProcessedAsync(string webhookId, CancellationToken cancellationToken = default) =>
        await _db.ProcessedWebhooks.AnyAsync(x => x.WebhookId == webhookId, cancellationToken);

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
