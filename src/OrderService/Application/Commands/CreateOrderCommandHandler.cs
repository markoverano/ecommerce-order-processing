using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using OrderService.Application.Validation;
using OrderService.Domain.Aggregates;
using OrderService.Domain.Repositories;

namespace OrderService.Application.Commands;

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, ServiceResponse<OrderId>>
{
    private readonly IOrderRepository _repository;
    private readonly CreateOrderCommandValidator _validator;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IOrderRepository repository,
        CreateOrderCommandValidator validator,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ServiceResponse<OrderId>> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return ServiceResponse<OrderId>.Failure("VALIDATION_FAILED", errors);
        }

        var items = command.Items
            .Select(i => new OrderItemData(i.ProductId, i.Quantity, i.UnitPrice))
            .ToList()
            .AsReadOnly();

        var order = Order.Create(command.CustomerId, items, command.ShippingAddress, command.CorrelationId);

        await _repository.SaveAsync(order, cancellationToken);

        _logger.LogInformation(
            "Order {OrderId} created for customer {CustomerId} with {ItemCount} items. CorrelationId={CorrelationId}",
            order.OrderId, command.CustomerId.Value, items.Count, command.CorrelationId);

        return ServiceResponse<OrderId>.Success(order.OrderId);
    }
}
