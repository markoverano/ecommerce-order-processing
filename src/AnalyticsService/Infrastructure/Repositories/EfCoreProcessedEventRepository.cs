using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using AnalyticsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Infrastructure.Repositories;

public class EfCoreProcessedEventRepository : EfCoreAnalyticsRepository<ProcessedEvent>, IProcessedEventRepository
{
    public EfCoreProcessedEventRepository(AnalyticsDbContext context) : base(context)
    {
    }

    public async Task<bool> IsProcessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(e => e.EventId == eventId, cancellationToken);
    }

    public async Task<ProcessedEvent?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
    }
}
