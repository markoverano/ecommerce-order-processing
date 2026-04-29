using ECommerceOrderProcessing.Shared.ValueObjects;
using OrderService.Domain.Exceptions;

namespace OrderService.Domain.Entities;

public sealed class OrderItem
{
    public ProductId ProductId { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }

    public Money LineTotal => Money.Create(UnitPrice.Amount * Quantity, UnitPrice.Currency);

    private OrderItem() { }

    internal static OrderItem Create(ProductId productId, int quantity, Money unitPrice)
    {
        if (quantity <= 0)
            throw new InvalidOrderException("Item quantity must be positive.");
        return new OrderItem { ProductId = productId, Quantity = quantity, UnitPrice = unitPrice };
    }
}
