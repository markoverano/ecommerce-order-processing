using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.Utilities;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;

namespace ECommerceOrderProcessing.Shared.Commands;

/// <summary>Initiates a new order. Handled by Order Service.</summary>
public sealed record CreateOrderCommand : IRequest<ServiceResponse<OrderId>>, IIdempotentCommand
{
    public IReadOnlyList<OrderItemRequest> Items { get; init; }
    public ShippingAddress ShippingAddress { get; init; }
    public IdempotencyKey IdempotencyKey { get; init; }
    public Guid CorrelationId { get; init; }

    public CreateOrderCommand(IReadOnlyList<OrderItemRequest> items, ShippingAddress shippingAddress, IdempotencyKey idempotencyKey, Guid correlationId)
    {
        Items = items;
        ShippingAddress = shippingAddress;
        IdempotencyKey = idempotencyKey;
        CorrelationId = correlationId;
    }

    string IIdempotentCommand.GetIdempotencyKey() => IdempotencyKey.Value;
}

public sealed record OrderItemRequest(ProductId ProductId, int Quantity, Money UnitPrice);
