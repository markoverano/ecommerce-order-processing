namespace SagaOrchestrator.Domain.Exceptions;

public sealed class InvalidSagaTransitionException : Exception
{
    public InvalidSagaTransitionException(string message) : base(message) { }
}
