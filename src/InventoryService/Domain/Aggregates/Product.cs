using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Domain.Exceptions;

namespace InventoryService.Domain.Aggregates;

public sealed class Product
{
    public ProductId ProductId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int AvailableQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }

    private Product() { }

    public static Product From(ProductId productId, string name, int availableQuantity, int reservedQuantity)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (availableQuantity < 0)
            throw new StockReservationException("Available quantity cannot be negative.");
        if (reservedQuantity < 0)
            throw new StockReservationException("Reserved quantity cannot be negative.");

        return new Product
        {
            ProductId = productId,
            Name = name,
            AvailableQuantity = availableQuantity,
            ReservedQuantity = reservedQuantity
        };
    }

    // Returns true if reservation succeeded; false if insufficient stock.
    public bool TryReserve(int quantity)
    {
        if (quantity <= 0)
            throw new StockReservationException($"Reservation quantity must be positive, got {quantity}.");
        if (AvailableQuantity < quantity)
            return false;

        AvailableQuantity -= quantity;
        ReservedQuantity += quantity;
        return true;
    }

    public void Release(int quantity)
    {
        if (quantity <= 0)
            throw new StockReservationException($"Release quantity must be positive, got {quantity}.");
        if (ReservedQuantity < quantity)
            throw new StockReservationException($"Cannot release {quantity} units; only {ReservedQuantity} are reserved.");

        ReservedQuantity -= quantity;
        AvailableQuantity += quantity;
    }
}
