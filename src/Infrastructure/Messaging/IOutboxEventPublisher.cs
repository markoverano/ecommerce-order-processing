namespace ECommerceOrderProcessing.Infrastructure.Messaging;

/// <summary>
/// Raw string-based publisher used exclusively by OutboxPublisher to re-emit stored events
/// without deserializing them. Keeps the typed IEventPublisher surface clean.
/// </summary>
public interface IOutboxEventPublisher
{
    Task PublishAsync(string eventType, string eventData, string routingKey, CancellationToken cancellationToken = default);
}
