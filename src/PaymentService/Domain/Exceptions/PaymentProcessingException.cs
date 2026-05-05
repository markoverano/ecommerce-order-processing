namespace PaymentService.Domain.Exceptions;

public sealed class PaymentProcessingException : Exception
{
    public PaymentProcessingException(string message) : base(message) { }
    public PaymentProcessingException(string message, Exception inner) : base(message, inner) { }
}
