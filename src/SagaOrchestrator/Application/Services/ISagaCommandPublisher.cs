using ECommerceOrderProcessing.Shared.Commands;

namespace SagaOrchestrator.Application.Services;

/// <summary>Publishes commands to downstream service queues as part of saga orchestration.</summary>
public interface ISagaCommandPublisher
{
    Task PublishProcessPaymentAsync(ProcessPaymentCommand command, CancellationToken cancellationToken = default);
    Task PublishReserveStockAsync(ReserveStockCommand command, CancellationToken cancellationToken = default);
    Task PublishCreateShipmentAsync(CreateShipmentCommand command, CancellationToken cancellationToken = default);
    Task PublishNotifyCustomerAsync(NotifyCustomerCommand command, CancellationToken cancellationToken = default);
    Task PublishRefundPaymentAsync(RefundPaymentCommand command, CancellationToken cancellationToken = default);
    Task PublishReleaseStockAsync(ReleaseStockCommand command, CancellationToken cancellationToken = default);
}
