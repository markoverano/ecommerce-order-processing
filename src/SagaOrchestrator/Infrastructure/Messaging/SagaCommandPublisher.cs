using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.Messaging;
using ECommerceOrderProcessing.Infrastructure.Serialization;
using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Domain;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Application.Services;

namespace SagaOrchestrator.Infrastructure.Messaging;

/// <summary>
/// Publishes saga commands to the order.events topic exchange using service-specific routing keys.
/// Each downstream service binds its command queue to the appropriate routing key.
/// </summary>
public sealed class SagaCommandPublisher : ISagaCommandPublisher
{
    private readonly IOutboxEventPublisher _publisher;
    private readonly ILogger<SagaCommandPublisher> _logger;

    public SagaCommandPublisher(IOutboxEventPublisher publisher, ILogger<SagaCommandPublisher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public Task PublishProcessPaymentAsync(ProcessPaymentCommand command, CancellationToken cancellationToken = default) =>
        PublishCommandAsync(command, "command.process-payment", cancellationToken);

    public Task PublishReserveStockAsync(ReserveStockCommand command, CancellationToken cancellationToken = default) =>
        PublishCommandAsync(command, "command.reserve-stock", cancellationToken);

    public Task PublishCreateShipmentAsync(CreateShipmentCommand command, CancellationToken cancellationToken = default) =>
        PublishCommandAsync(command, "command.create-shipment", cancellationToken);

    public Task PublishNotifyCustomerAsync(NotifyCustomerCommand command, CancellationToken cancellationToken = default) =>
        PublishCommandAsync(command, "command.notify-customer", cancellationToken);

    public Task PublishRefundPaymentAsync(RefundPaymentCommand command, CancellationToken cancellationToken = default) =>
        PublishCommandAsync(command, "command.refund-payment", cancellationToken);

    public Task PublishReleaseStockAsync(ReleaseStockCommand command, CancellationToken cancellationToken = default) =>
        PublishCommandAsync(command, "command.release-stock", cancellationToken);

    private async Task PublishCommandAsync<T>(T command, string routingKey, CancellationToken cancellationToken)
    {
        var eventData = JsonSerializer.Serialize(command, command!.GetType(), InfrastructureJsonOptions.Default);
        await _publisher.PublishAsync(typeof(T).Name, eventData, routingKey, cancellationToken);

        _logger.LogDebug("Published command {CommandType} with routing key {RoutingKey}", typeof(T).Name, routingKey);
    }
}
