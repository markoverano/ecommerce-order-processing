using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Saga;

public sealed record SagaCompleted : DomainEvent
{
    public Guid SagaId { get; init; }
    public OrderId OrderId { get; init; }

    public SagaCompleted(Guid sagaId, OrderId orderId, int version, Guid correlationId)
        : base(sagaId, version, correlationId)
    {
        SagaId = sagaId;
        OrderId = orderId;
    }
}
