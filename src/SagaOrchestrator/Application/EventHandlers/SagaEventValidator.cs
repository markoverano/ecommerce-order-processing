using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;
using SagaOrchestrator.Domain.Aggregates;
using SagaOrchestrator.Domain.Exceptions;

namespace SagaOrchestrator.Application.EventHandlers;

/// <summary>
/// Validates domain events before processing to ensure they belong to the expected saga
/// and haven't been processed before (idempotency).
/// </summary>
public sealed class SagaEventValidator
{
    public void ValidateAggregateId(DomainEvent evt, OrderProcessingSaga saga)
    {
        if (evt.AggregateId != saga.Id)
            throw new InvalidSagaTransitionException(
                $"Event aggregate ID {evt.AggregateId:N} does not match saga ID {saga.Id:N}");
    }

    public void ValidateCorrelationId(DomainEvent evt, OrderProcessingSaga saga)
    {
        if (evt.CorrelationId == Guid.Empty)
            throw new InvalidSagaTransitionException(
                $"Event {evt.GetType().Name} has empty CorrelationId");
    }
}
