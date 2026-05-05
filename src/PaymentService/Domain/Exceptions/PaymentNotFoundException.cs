using ECommerceOrderProcessing.Shared.ValueObjects;

namespace PaymentService.Domain.Exceptions;

public sealed class PaymentNotFoundException : Exception
{
    public PaymentNotFoundException(PaymentId paymentId)
        : base($"Payment {paymentId} was not found.") { }

    public PaymentNotFoundException(Guid id)
        : base($"Payment {id} was not found.") { }
}
