using ECommerceOrderProcessing.Infrastructure.Messaging.AzureServiceBus;
using ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;
using ECommerceOrderProcessing.Shared.Domain;
using Microsoft.Extensions.Logging;

namespace ECommerceOrderProcessing.Infrastructure.Messaging;

/// <summary>
/// Publishes to RabbitMQ first. On failure, falls back to Azure Service Bus.
/// The 500ms timeout ensures fast fail so the outbox does not block on a dead broker.
/// </summary>
public sealed class HybridPublisher : IEventPublisher
{
    private readonly RabbitMqPublisher _primary;
    private readonly AzureServiceBusPublisher _fallback;
    private readonly ILogger<HybridPublisher> _logger;
    private static readonly TimeSpan PrimaryTimeout = TimeSpan.FromMilliseconds(500);

    public HybridPublisher(RabbitMqPublisher primary, AzureServiceBusPublisher fallback, ILogger<HybridPublisher> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T domainEvent, string routingKey, CancellationToken cancellationToken = default) where T : DomainEvent
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(PrimaryTimeout);

        try
        {
            await _primary.PublishAsync(domainEvent, routingKey, cts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException { CancellationToken.IsCancellationRequested: true } oce || oce.CancellationToken != cancellationToken)
        {
            _logger.LogWarning(ex, "RabbitMQ publish failed; switching to Azure Service Bus fallback for {EventType}", typeof(T).Name);
            await _fallback.PublishAsync(domainEvent, routingKey, cancellationToken);
        }
    }

    public async Task PublishAsync(string eventType, string eventData, string routingKey, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(PrimaryTimeout);

        try
        {
            await _primary.PublishAsync(eventType, eventData, routingKey, cts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException { CancellationToken.IsCancellationRequested: true } oce || oce.CancellationToken != cancellationToken)
        {
            _logger.LogWarning(ex, "RabbitMQ publish failed; switching to Azure Service Bus fallback for {EventType}", eventType);
            await _fallback.PublishAsync(eventType, eventData, routingKey, cancellationToken);
        }
    }
}
