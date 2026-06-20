using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using AnalyticsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Infrastructure.Repositories;

public class EfCoreSalesSummaryRepository : EfCoreAnalyticsRepository<SalesSummary>, ISalesSummaryRepository
{
    public EfCoreSalesSummaryRepository(AnalyticsDbContext context) : base(context)
    {
    }

    public async Task<SalesSummary?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(s => s.Date == date, cancellationToken);
    }

    public async Task<IEnumerable<SalesSummary>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.Date >= startDate && s.Date <= endDate)
            .OrderBy(s => s.Date)
            .ToListAsync(cancellationToken);
    }
}
