using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Application.Commands;
using InventoryService.Application.Validation;
using InventoryService.Domain.Aggregates;
using InventoryService.Domain.Enums;
using InventoryService.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace InventoryService.Application.Tests;

public sealed class ReserveStockCommandHandlerTests
{
    private static readonly OrderId SomeOrder = new(Guid.NewGuid());
    private static readonly ProductId SomeProduct = new(Guid.NewGuid());
    private const string SomeProductName = "Widget A";

    private static ReserveStockCommand BuildCommand(int quantity = 5) =>
        new(SomeOrder, new[] { new StockReservationItem(SomeProduct, quantity) }, Guid.NewGuid());

    [Fact]
    public async Task Handle_WhenSufficientStock_SavesReservedReservationAndReturnsId()
    {
        var reservationRepo = new Mock<IStockReservationRepository>();
        var productRepo = new Mock<IProductRepository>();

        var product = Product.From(SomeProduct, SomeProductName, 100, 0);
        productRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<ProductId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { product });

        StockReservation? saved = null;
        reservationRepo
            .Setup(r => r.SaveAsync(It.IsAny<StockReservation>(), It.IsAny<IReadOnlyList<Product>>(), It.IsAny<CancellationToken>()))
            .Callback<StockReservation, IReadOnlyList<Product>, CancellationToken>((r, _, _) => saved = r)
            .Returns(Task.CompletedTask);

        var handler = new ReserveStockCommandHandler(
            reservationRepo.Object, productRepo.Object,
            new ReserveStockCommandValidator(), NullLogger<ReserveStockCommandHandler>.Instance);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(saved);
        Assert.Equal(ReservationStatus.Reserved, saved!.Status);
    }

    [Fact]
    public async Task Handle_WhenSufficientStock_ReturnsReservationId()
    {
        var reservationRepo = new Mock<IStockReservationRepository>();
        var productRepo = new Mock<IProductRepository>();

        productRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<ProductId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Product.From(SomeProduct, SomeProductName, 100, 0) });
        reservationRepo
            .Setup(r => r.SaveAsync(It.IsAny<StockReservation>(), It.IsAny<IReadOnlyList<Product>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ReserveStockCommandHandler(
            reservationRepo.Object, productRepo.Object,
            new ReserveStockCommandValidator(), NullLogger<ReserveStockCommandHandler>.Instance);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Data!.Value);
    }

    [Fact]
    public async Task Handle_WhenInsufficientStock_SavesFailedReservationAndReturnsFailure()
    {
        var reservationRepo = new Mock<IStockReservationRepository>();
        var productRepo = new Mock<IProductRepository>();

        var product = Product.From(SomeProduct, SomeProductName, 2, 0);
        productRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<ProductId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { product });

        StockReservation? saved = null;
        reservationRepo
            .Setup(r => r.SaveAsync(It.IsAny<StockReservation>(), It.IsAny<IReadOnlyList<Product>>(), It.IsAny<CancellationToken>()))
            .Callback<StockReservation, IReadOnlyList<Product>, CancellationToken>((r, _, _) => saved = r)
            .Returns(Task.CompletedTask);

        var handler = new ReserveStockCommandHandler(
            reservationRepo.Object, productRepo.Object,
            new ReserveStockCommandValidator(), NullLogger<ReserveStockCommandHandler>.Instance);

        var result = await handler.Handle(BuildCommand(quantity: 5), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("OUT_OF_STOCK", result.Error?.Code);
        Assert.NotNull(saved);
        Assert.Equal(ReservationStatus.Failed, saved!.Status);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ReturnsFailure()
    {
        var reservationRepo = new Mock<IStockReservationRepository>();
        var productRepo = new Mock<IProductRepository>();

        productRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<ProductId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Product>());

        var handler = new ReserveStockCommandHandler(
            reservationRepo.Object, productRepo.Object,
            new ReserveStockCommandValidator(), NullLogger<ReserveStockCommandHandler>.Instance);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("PRODUCT_NOT_FOUND", result.Error?.Code);
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ReturnsValidationFailure()
    {
        var reservationRepo = new Mock<IStockReservationRepository>();
        var productRepo = new Mock<IProductRepository>();

        var invalidCommand = new ReserveStockCommand(SomeOrder, Array.Empty<StockReservationItem>(), Guid.NewGuid());

        var handler = new ReserveStockCommandHandler(
            reservationRepo.Object, productRepo.Object,
            new ReserveStockCommandValidator(), NullLogger<ReserveStockCommandHandler>.Instance);

        var result = await handler.Handle(invalidCommand, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_FAILED", result.Error?.Code);
    }
}
