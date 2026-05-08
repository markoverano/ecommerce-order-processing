using ECommerceOrderProcessing.Shared.Commands;
using FluentValidation;

namespace InventoryService.Application.Validation;

public sealed class ReserveStockCommandValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockCommandValidator()
    {
        RuleFor(x => x.OrderId.Value).NotEmpty().WithMessage("OrderId is required.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId.Value).NotEmpty().WithMessage("ProductId is required.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Item quantity must be positive.");
        });
    }
}
