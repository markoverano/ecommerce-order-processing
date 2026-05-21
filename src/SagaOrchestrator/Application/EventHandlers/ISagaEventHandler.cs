using ECommerceOrderProcessing.Shared.Domain;

namespace SagaOrchestrator.Application.EventHandlers;

public interface ISagaEventHandler
{
    string EventTypeName { get; }
    Type EventType { get; }
    Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken);
}
