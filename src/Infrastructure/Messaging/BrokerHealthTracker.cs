namespace ECommerceOrderProcessing.Infrastructure.Messaging;

/// <summary>
/// Singleton that tracks whether publish traffic is currently routed through the primary broker (RabbitMQ)
/// or the fallback broker (Azure Service Bus). Updated by <see cref="HybridPublisher"/> on each transition.
/// Queried by health checks to surface broker degradation without coupling health-check code to publisher logic.
/// </summary>
public sealed class BrokerHealthTracker
{
    private int _state; // 0 = primary, 1 = fallback; updated via Interlocked to avoid locks on the hot path
    private DateTimeOffset? _fallbackActivatedAt;

    public bool IsUsingFallback => Volatile.Read(ref _state) == 1;
    public DateTimeOffset? FallbackActivatedAt => _fallbackActivatedAt;

    public void RecordPrimarySuccess()
    {
        if (Interlocked.CompareExchange(ref _state, 0, 1) == 1)
            _fallbackActivatedAt = null;
    }

    public void RecordFallbackActivated()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
            _fallbackActivatedAt = DateTimeOffset.UtcNow;
    }
}
