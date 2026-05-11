using ECommerceOrderProcessing.Shared.Commands;
using FluentValidation;

namespace NotificationService.Application.Validation;

public sealed class NotifyCustomerCommandValidator : AbstractValidator<NotifyCustomerCommand>
{
    public NotifyCustomerCommandValidator()
    {
        RuleFor(x => x.OrderId.Value).NotEmpty();
        RuleFor(x => x.CustomerId.Value).NotEmpty();
        RuleFor(x => x.NotificationType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CorrelationId).NotEmpty();
    }
}
