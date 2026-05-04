namespace SagaOrchestrator.Domain.Exceptions;

public sealed class SagaNotFoundException : Exception
{
    public SagaNotFoundException(Guid orderId)
        : base($"Saga for order {orderId} not found.") { }
}
