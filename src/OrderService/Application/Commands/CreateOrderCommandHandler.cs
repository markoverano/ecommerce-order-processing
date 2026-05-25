using ECommerceOrderProcessing.Shared.Auth;
using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.Validation;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using OrderService.Application.Metrics;
using OrderService.Application.Validation;
using OrderService.Domain.Aggregates;
using OrderService.Domain.Repositories;

namespace OrderService.Application.Commands;

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, ServiceResponse<OrderId>>
{
    private readonly IOrderRepository _repository;
    private readonly CreateOrderCommandValidator _validator;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IOrderRepository repository,
        CreateOrderCommandValidator validator,
        ICurrentUserAccessor currentUserAccessor,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _repository = repository;
        _validator = validator;
        _currentUserAccessor = currentUserAccessor;
        _logger = logger;
    }

    public async Task<ServiceResponse<OrderId>> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            OrderMetrics.OrderCreationFailed.Inc();
            return validation.ToFailureResponse<OrderId>();
        }

        // The [Authorize] attribute guarantees this is non-null on any reachable code path.
        var user = _currentUserAccessor.GetCurrentUser()
            ?? throw new InvalidOperationException("Authenticated user context is missing from an authorized endpoint.");

        var items = command.Items
            .Select(i => new OrderItemData(i.ProductId, i.Quantity, i.UnitPrice))
            .ToList()
            .AsReadOnly();

        var order = Order.Create(user.UserId, items, command.ShippingAddress, command.CorrelationId);

        await _repository.SaveAsync(order, cancellationToken);

        OrderMetrics.OrdersCreated.Inc();

        _logger.LogInformation(
            "Order {OrderId} created for customer {CustomerId} with {ItemCount} items. CorrelationId={CorrelationId}",
            order.OrderId, user.UserId.Value, items.Count, command.CorrelationId);

        return ServiceResponse<OrderId>.Success(order.OrderId);
    }
}
