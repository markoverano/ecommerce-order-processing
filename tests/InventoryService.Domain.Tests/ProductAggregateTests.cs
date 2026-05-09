using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Domain.Aggregates;
using InventoryService.Domain.Exceptions;
using Xunit;

namespace InventoryService.Domain.Tests;

public sealed class ProductAggregateTests
{
    private static readonly ProductId SomeProduct = new(Guid.NewGuid());
    private const string SomeName = "Widget A";

    [Fact]
    public void From_WithValidData_CreatesProduct()
    {
        var product = Product.From(SomeProduct, SomeName, 100, 0);

        Assert.Equal(SomeProduct, product.ProductId);
        Assert.Equal(100, product.AvailableQuantity);
        Assert.Equal(0, product.ReservedQuantity);
    }

    [Fact]
    public void From_WithNegativeAvailableQuantity_ThrowsStockReservationException()
    {
        Assert.Throws<StockReservationException>(() =>
            Product.From(SomeProduct, SomeName, -1, 0));
    }

    [Fact]
    public void From_WithNegativeReservedQuantity_ThrowsStockReservationException()
    {
        Assert.Throws<StockReservationException>(() =>
            Product.From(SomeProduct, SomeName, 10, -1));
    }

    [Fact]
    public void TryReserve_WhenSufficientStock_ReturnsTrueAndDecrementsAvailable()
    {
        var product = Product.From(SomeProduct, SomeName, 100, 0);

        var result = product.TryReserve(10);

        Assert.True(result);
        Assert.Equal(90, product.AvailableQuantity);
        Assert.Equal(10, product.ReservedQuantity);
    }

    [Fact]
    public void TryReserve_WhenInsufficientStock_ReturnsFalseAndLeavesQuantitiesUnchanged()
    {
        var product = Product.From(SomeProduct, SomeName, 5, 0);

        var result = product.TryReserve(10);

        Assert.False(result);
        Assert.Equal(5, product.AvailableQuantity);
        Assert.Equal(0, product.ReservedQuantity);
    }

    [Fact]
    public void TryReserve_ExactlyAvailableQuantity_ReturnsTrue()
    {
        var product = Product.From(SomeProduct, SomeName, 10, 0);

        var result = product.TryReserve(10);

        Assert.True(result);
        Assert.Equal(0, product.AvailableQuantity);
        Assert.Equal(10, product.ReservedQuantity);
    }

    [Fact]
    public void TryReserve_WithZeroQuantity_ThrowsStockReservationException()
    {
        var product = Product.From(SomeProduct, SomeName, 100, 0);

        Assert.Throws<StockReservationException>(() => product.TryReserve(0));
    }

    [Fact]
    public void Release_DecrementsReservedAndIncrementsAvailable()
    {
        var product = Product.From(SomeProduct, SomeName, 90, 10);

        product.Release(5);

        Assert.Equal(95, product.AvailableQuantity);
        Assert.Equal(5, product.ReservedQuantity);
    }

    [Fact]
    public void Release_ExactlyReservedQuantity_SetsReservedToZero()
    {
        var product = Product.From(SomeProduct, SomeName, 90, 10);

        product.Release(10);

        Assert.Equal(100, product.AvailableQuantity);
        Assert.Equal(0, product.ReservedQuantity);
    }

    [Fact]
    public void Release_MoreThanReserved_ThrowsStockReservationException()
    {
        var product = Product.From(SomeProduct, SomeName, 90, 5);

        Assert.Throws<StockReservationException>(() => product.Release(10));
    }

    [Fact]
    public void Release_WithZeroQuantity_ThrowsStockReservationException()
    {
        var product = Product.From(SomeProduct, SomeName, 90, 10);

        Assert.Throws<StockReservationException>(() => product.Release(0));
    }

    [Fact]
    public void TryReserve_MultipleTimes_AccumulatesCorrectly()
    {
        var product = Product.From(SomeProduct, SomeName, 100, 0);

        product.TryReserve(30);
        product.TryReserve(20);

        Assert.Equal(50, product.AvailableQuantity);
        Assert.Equal(50, product.ReservedQuantity);
    }
}
