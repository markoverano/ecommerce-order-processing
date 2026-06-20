using AnalyticsService.Domain.Entities;

namespace AnalyticsService.Domain.Repositories;

public interface ISalesSummaryRepository : IAnalyticsRepository<SalesSummary>
{
    Task<SalesSummary?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IEnumerable<SalesSummary>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
}
