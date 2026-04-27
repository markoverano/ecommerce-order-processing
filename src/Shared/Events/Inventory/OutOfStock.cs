using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Inventory;

/// <summary>Published when at least one ordered product cannot be reserved.</summary>
public sealed record OutOfStock : DomainEvent
{
    public OrderId OrderId { get; init; }
    public ProductId ProductId { get; init; }
    public int RequestedQuantity { get; init; }
    public int AvailableQuantity { get; init; }

    public OutOfStock(OrderId orderId, ProductId productId, int requestedQuantity, int availableQuantity, int version, Guid correlationId)
        : base(orderId.Value, version, correlationId)
    {
        OrderId = orderId;
        ProductId = productId;
        RequestedQuantity = requestedQuantity;
        AvailableQuantity = availableQuantity;
    }
}
