using ECommerceOrderProcessing.Shared.Commands;
using FluentValidation;

namespace ShippingService.Application.Validation;

public sealed class CreateShipmentCommandValidator : AbstractValidator<CreateShipmentCommand>
{
    public CreateShipmentCommandValidator()
    {
        RuleFor(x => x.OrderId.Value).NotEmpty().WithMessage("OrderId is required.");
        RuleFor(x => x.CustomerId.Value).NotEmpty().WithMessage("CustomerId is required.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one shipment item is required.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Item quantity must be greater than zero.");
            item.RuleFor(i => i.Description).NotEmpty().WithMessage("Item description is required.");
        });
        RuleFor(x => x.ShippingAddress.Line1).NotEmpty().WithMessage("Shipping address line 1 is required.");
        RuleFor(x => x.ShippingAddress.City).NotEmpty().WithMessage("Shipping address city is required.");
        RuleFor(x => x.ShippingAddress.PostalCode).NotEmpty().WithMessage("Shipping address postal code is required.");
        RuleFor(x => x.ShippingAddress.CountryCode).NotEmpty().WithMessage("Shipping address country code is required.");
    }
}
