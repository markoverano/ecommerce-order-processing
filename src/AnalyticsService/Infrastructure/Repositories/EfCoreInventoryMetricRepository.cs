using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using AnalyticsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Infrastructure.Repositories;

public class EfCoreInventoryMetricRepository : EfCoreAnalyticsRepository<InventoryMetric>, IInventoryMetricRepository
{
    public EfCoreInventoryMetricRepository(AnalyticsDbContext context) : base(context)
    {
    }

    public async Task<InventoryMetric?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(i => i.ReservationId == reservationId, cancellationToken);
    }

    public async Task<IEnumerable<InventoryMetric>> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(i => i.ProductId == productId)
            .OrderByDescending(i => i.ReleasedAt ?? DateTime.UtcNow)
            .ToListAsync(cancellationToken);
    }
}
