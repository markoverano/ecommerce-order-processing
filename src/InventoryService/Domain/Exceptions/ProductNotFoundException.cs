using ECommerceOrderProcessing.Shared.ValueObjects;

namespace InventoryService.Domain.Exceptions;

public sealed class ProductNotFoundException : Exception
{
    public ProductNotFoundException(ProductId productId)
        : base($"Product {productId} was not found in inventory.") { }
}
