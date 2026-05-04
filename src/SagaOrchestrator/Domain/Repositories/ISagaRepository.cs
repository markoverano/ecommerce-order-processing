using ECommerceOrderProcessing.Shared.ValueObjects;
using SagaOrchestrator.Domain.Aggregates;

namespace SagaOrchestrator.Domain.Repositories;

public interface ISagaRepository
{
    Task<OrderProcessingSaga?> GetByOrderIdAsync(OrderId orderId, CancellationToken cancellationToken = default);
    Task SaveAsync(OrderProcessingSaga saga, CancellationToken cancellationToken = default);
}
