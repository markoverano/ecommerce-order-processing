using AnalyticsService.Domain.Entities;

namespace AnalyticsService.Domain.Repositories;

public interface IPaymentMetricRepository : IAnalyticsRepository<PaymentMetric>
{
    Task<PaymentMetric?> GetByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentMetric>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentMetric>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}
