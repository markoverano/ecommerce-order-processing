using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Models;

/// <summary>Immutable line-item snapshot carried inside order events and commands.</summary>
public sealed record OrderItemData(ProductId ProductId, int Quantity, Money UnitPrice)
{
    public Money LineTotal => Money.Create(UnitPrice.Amount * Quantity, UnitPrice.Currency);
}
