using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Saga;

public sealed record SagaStarted : DomainEvent
{
    public Guid SagaId { get; init; }
    public OrderId OrderId { get; init; }

    public SagaStarted(Guid sagaId, OrderId orderId, Guid correlationId)
        : base(sagaId, 1, correlationId)
    {
        SagaId = sagaId;
        OrderId = orderId;
    }
}
