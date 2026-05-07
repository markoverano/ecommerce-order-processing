using ECommerceOrderProcessing.Shared.Commands;
using FluentValidation;

namespace PaymentService.Application.Validation;

public sealed class ProcessPaymentCommandValidator : AbstractValidator<ProcessPaymentCommand>
{
    public ProcessPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId.Value).NotEmpty().WithMessage("OrderId is required.");
        RuleFor(x => x.CustomerId.Value).NotEmpty().WithMessage("CustomerId is required.");
        RuleFor(x => x.Amount.Amount).GreaterThan(0).WithMessage("Payment amount must be positive.");
        RuleFor(x => x.Amount.Currency).NotEmpty().WithMessage("Currency is required.");
        RuleFor(x => x.PaymentMethodId).NotEmpty().WithMessage("PaymentMethodId is required.");
    }
}
