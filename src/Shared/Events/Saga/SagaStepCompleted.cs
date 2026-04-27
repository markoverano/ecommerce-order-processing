using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Saga;

public sealed record SagaStepCompleted : DomainEvent
{
    public Guid SagaId { get; init; }
    public OrderId OrderId { get; init; }
    public string Step { get; init; }

    public SagaStepCompleted(Guid sagaId, OrderId orderId, string step, int version, Guid correlationId)
        : base(sagaId, version, correlationId)
    {
        SagaId = sagaId;
        OrderId = orderId;
        Step = step;
    }
}
