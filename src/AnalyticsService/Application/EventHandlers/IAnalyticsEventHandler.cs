using ECommerceOrderProcessing.Shared.Domain;

namespace AnalyticsService.Application.EventHandlers;

public interface IAnalyticsEventHandler
{
    string EventTypeName { get; }
    Type EventType { get; }
    Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken);
}
