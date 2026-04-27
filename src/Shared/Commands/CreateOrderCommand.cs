using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.Utilities;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;

namespace ECommerceOrderProcessing.Shared.Commands;

/// <summary>Initiates a new order. Handled by Order Service.</summary>
public sealed record CreateOrderCommand : IRequest<ServiceResponse<OrderId>>
{
    public CustomerId CustomerId { get; init; }
    public IReadOnlyList<OrderItemRequest> Items { get; init; }
    public ShippingAddress ShippingAddress { get; init; }
    public IdempotencyKey IdempotencyKey { get; init; }
    public Guid CorrelationId { get; init; }

    public CreateOrderCommand(CustomerId customerId, IReadOnlyList<OrderItemRequest> items, ShippingAddress shippingAddress, IdempotencyKey idempotencyKey, Guid correlationId)
    {
        CustomerId = customerId;
        Items = items;
        ShippingAddress = shippingAddress;
        IdempotencyKey = idempotencyKey;
        CorrelationId = correlationId;
    }
}

public sealed record OrderItemRequest(ProductId ProductId, int Quantity);
