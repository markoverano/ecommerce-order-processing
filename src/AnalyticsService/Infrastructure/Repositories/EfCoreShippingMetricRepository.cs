using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using AnalyticsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Infrastructure.Repositories;

public class EfCoreShippingMetricRepository : EfCoreAnalyticsRepository<ShippingMetric>, IShippingMetricRepository
{
    public EfCoreShippingMetricRepository(AnalyticsDbContext context) : base(context)
    {
    }

    public async Task<ShippingMetric?> GetByShipmentIdAsync(Guid shipmentId, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(s => s.ShipmentId == shipmentId, cancellationToken);
    }

    public async Task<IEnumerable<ShippingMetric>> GetByCarrierAsync(string carrier, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.Carrier == carrier)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ShippingMetric>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.CreatedAt >= startDate && s.CreatedAt <= endDate)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
