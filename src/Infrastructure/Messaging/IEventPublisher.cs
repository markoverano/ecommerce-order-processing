using ECommerceOrderProcessing.Shared.Domain;

namespace ECommerceOrderProcessing.Infrastructure.Messaging;

/// <summary>Publishes domain events to the message broker.</summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(T domainEvent, string routingKey, CancellationToken cancellationToken = default) where T : DomainEvent;
    Task PublishAsync(string eventType, string eventData, string routingKey, CancellationToken cancellationToken = default);
}
