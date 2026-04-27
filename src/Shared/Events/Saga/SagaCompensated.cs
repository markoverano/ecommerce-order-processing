using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Saga;

public sealed record SagaCompensated : DomainEvent
{
    public Guid SagaId { get; init; }
    public OrderId OrderId { get; init; }
    public string Reason { get; init; }

    public SagaCompensated(Guid sagaId, OrderId orderId, string reason, int version, Guid correlationId)
        : base(sagaId, version, correlationId)
    {
        SagaId = sagaId;
        OrderId = orderId;
        Reason = reason;
    }
}
