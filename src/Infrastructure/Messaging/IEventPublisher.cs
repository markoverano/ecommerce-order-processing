using ECommerceOrderProcessing.Shared.Domain;

namespace ECommerceOrderProcessing.Infrastructure.Messaging;

/// <summary>Publishes typed domain events to the message broker.</summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(T domainEvent, string routingKey, CancellationToken cancellationToken = default) where T : DomainEvent;
}
