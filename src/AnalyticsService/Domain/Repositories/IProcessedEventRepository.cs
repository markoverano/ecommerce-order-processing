using AnalyticsService.Domain.Entities;

namespace AnalyticsService.Domain.Repositories;

public interface IProcessedEventRepository : IAnalyticsRepository<ProcessedEvent>
{
    Task<bool> IsProcessedAsync(string eventId, CancellationToken cancellationToken = default);
    Task<ProcessedEvent?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
}
