using ECommerceOrderProcessing.Shared.Commands;
using FluentValidation;

namespace OrderService.Application.Validation;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId.Value).NotEmpty().WithMessage("ProductId is required.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantity must be positive.");
            item.RuleFor(i => i.UnitPrice.Amount).GreaterThan(0).WithMessage("Unit price must be positive.");
            item.RuleFor(i => i.UnitPrice.Currency).NotEmpty().WithMessage("Currency is required.");
        });
        RuleFor(x => x.ShippingAddress.Line1).NotEmpty().WithMessage("Shipping address line 1 is required.");
        RuleFor(x => x.ShippingAddress.City).NotEmpty().WithMessage("City is required.");
        RuleFor(x => x.ShippingAddress.State).NotEmpty().WithMessage("State is required.");
        RuleFor(x => x.ShippingAddress.PostalCode).NotEmpty().WithMessage("Postal code is required.");
        RuleFor(x => x.ShippingAddress.CountryCode).NotEmpty().WithMessage("Country code is required.");
    }
}
