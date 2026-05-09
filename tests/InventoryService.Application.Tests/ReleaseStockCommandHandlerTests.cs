using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Application.Commands;
using InventoryService.Domain.Aggregates;
using InventoryService.Domain.Enums;
using InventoryService.Domain.Events;
using InventoryService.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace InventoryService.Application.Tests;

public sealed class ReleaseStockCommandHandlerTests
{
    private static readonly ReservationId SomeReservation = new(Guid.NewGuid());
    private static readonly OrderId SomeOrder = new(Guid.NewGuid());
    private static readonly ProductId SomeProduct = new(Guid.NewGuid());
    private const string SomeProductName = "Widget A";

    private static ReleaseStockCommand BuildCommand() =>
        new(SomeReservation, SomeOrder, Guid.NewGuid());

    private static StockReservation BuildReservedReservation()
    {
        var items = new[] { new StockReservationItem(SomeProduct, 5) };
        var reservation = StockReservation.Create(SomeOrder, items, Guid.NewGuid());
        reservation.ClearUncommittedEvents();
        return reservation;
    }

    [Fact]
    public async Task Handle_WhenReservationExists_ReleasesAndSavesWithReleasedStatus()
    {
        var reservationRepo = new Mock<IStockReservationRepository>();
        var productRepo = new Mock<IProductRepository>();

        var reservation = BuildReservedReservation();
        reservationRepo
            .Setup(r => r.GetByIdAsync(SomeReservation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        var product = Product.From(SomeProduct, SomeProductName, 95, 5);
        productRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<ProductId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { product });

        StockReservation? saved = null;
        reservationRepo
            .Setup(r => r.SaveAsync(It.IsAny<StockReservation>(), It.IsAny<IReadOnlyList<Product>>(), It.IsAny<CancellationToken>()))
            .Callback<StockReservation, IReadOnlyList<Product>, CancellationToken>((r, _, _) => saved = r)
            .Returns(Task.CompletedTask);

        var handler = new ReleaseStockCommandHandler(
            reservationRepo.Object, productRepo.Object,
            NullLogger<ReleaseStockCommandHandler>.Instance);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(saved);
        Assert.Equal(ReservationStatus.Released, saved!.Status);
    }

    [Fact]
    public async Task Handle_WhenReservationNotFound_ReturnsFailure()
    {
        var reservationRepo = new Mock<IStockReservationRepository>();
        var productRepo = new Mock<IProductRepository>();

        reservationRepo
            .Setup(r => r.GetByIdAsync(SomeReservation, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockReservation?)null);

        var handler = new ReleaseStockCommandHandler(
            reservationRepo.Object, productRepo.Object,
            NullLogger<ReleaseStockCommandHandler>.Instance);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("RESERVATION_NOT_FOUND", result.Error?.Code);
    }

    [Fact]
    public async Task Handle_WhenReleased_RestoresProductAvailableQuantity()
    {
        var reservationRepo = new Mock<IStockReservationRepository>();
        var productRepo = new Mock<IProductRepository>();

        var reservation = BuildReservedReservation();
        reservationRepo
            .Setup(r => r.GetByIdAsync(SomeReservation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        var product = Product.From(SomeProduct, SomeProductName, 95, 5);
        productRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<ProductId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { product });

        IReadOnlyList<Product>? savedProducts = null;
        reservationRepo
            .Setup(r => r.SaveAsync(It.IsAny<StockReservation>(), It.IsAny<IReadOnlyList<Product>>(), It.IsAny<CancellationToken>()))
            .Callback<StockReservation, IReadOnlyList<Product>, CancellationToken>((_, p, _) => savedProducts = p)
            .Returns(Task.CompletedTask);

        var handler = new ReleaseStockCommandHandler(
            reservationRepo.Object, productRepo.Object,
            NullLogger<ReleaseStockCommandHandler>.Instance);

        await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.NotNull(savedProducts);
        var updatedProduct = savedProducts!.First();
        Assert.Equal(100, updatedProduct.AvailableQuantity);
        Assert.Equal(0, updatedProduct.ReservedQuantity);
    }
}
