using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Webhooks;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Webhooks;

public sealed class EfCoreWebhookDeduplicator : IWebhookDeduplicator
{
    private readonly PaymentDbContext _db;

    public EfCoreWebhookDeduplicator(PaymentDbContext db)
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
