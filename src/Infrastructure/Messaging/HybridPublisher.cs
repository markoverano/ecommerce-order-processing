using ECommerceOrderProcessing.Shared.Domain;
using Microsoft.Extensions.Logging;

namespace ECommerceOrderProcessing.Infrastructure.Messaging;

/// <summary>
/// Publishes to the primary broker (RabbitMQ) first. If that fails or takes longer than 500 ms,
/// falls back to Azure Service Bus. <see cref="BrokerHealthTracker"/> is updated on every transition
/// so health checks can surface broker degradation independently of the publish path.
/// </summary>
public sealed class HybridPublisher : IEventPublisher, IOutboxEventPublisher
{
    private readonly IEventPublisher _primary;
    private readonly IEventPublisher _fallback;
    private readonly IOutboxEventPublisher _primaryRaw;
    private readonly IOutboxEventPublisher _fallbackRaw;
    private readonly BrokerHealthTracker _healthTracker;
    private readonly ILogger<HybridPublisher> _logger;
    private static readonly TimeSpan PrimaryTimeout = TimeSpan.FromMilliseconds(500);

    public HybridPublisher(
        IEventPublisher primary,
        IEventPublisher fallback,
        IOutboxEventPublisher primaryRaw,
        IOutboxEventPublisher fallbackRaw,
        BrokerHealthTracker healthTracker,
        ILogger<HybridPublisher> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _primaryRaw = primaryRaw;
        _fallbackRaw = fallbackRaw;
        _healthTracker = healthTracker;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T domainEvent, string routingKey, CancellationToken cancellationToken = default) where T : DomainEvent
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(PrimaryTimeout);

        try
        {
            await _primary.PublishAsync(domainEvent, routingKey, cts.Token);
            _healthTracker.RecordPrimarySuccess();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "RabbitMQ publish failed for {EventType}; activating Azure Service Bus fallback", typeof(T).Name);
            _healthTracker.RecordFallbackActivated();
            await _fallback.PublishAsync(domainEvent, routingKey, cancellationToken);
        }
    }

    public async Task PublishAsync(string eventType, string eventData, string routingKey, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(PrimaryTimeout);

        try
        {
            await _primaryRaw.PublishAsync(eventType, eventData, routingKey, cts.Token);
            _healthTracker.RecordPrimarySuccess();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "RabbitMQ publish failed for {EventType}; activating Azure Service Bus fallback", eventType);
            _healthTracker.RecordFallbackActivated();
            await _fallbackRaw.PublishAsync(eventType, eventData, routingKey, cancellationToken);
        }
    }
}
