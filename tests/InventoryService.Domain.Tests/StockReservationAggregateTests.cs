using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Domain.Aggregates;
using InventoryService.Domain.Enums;
using InventoryService.Domain.Events;
using InventoryService.Domain.Exceptions;
using Xunit;

namespace InventoryService.Domain.Tests;

public sealed class StockReservationAggregateTests
{
    private static readonly OrderId SomeOrder = new(Guid.NewGuid());
    private static readonly ProductId SomeProduct = new(Guid.NewGuid());
    private static readonly IReadOnlyList<StockReservationItem> SomeItems =
        new[] { new StockReservationItem(SomeProduct, 5) };

    [Fact]
    public void Create_WithValidData_SetsStatusReserved()
    {
        var reservation = StockReservation.Create(SomeOrder, SomeItems, Guid.NewGuid());

        Assert.Equal(ReservationStatus.Reserved, reservation.Status);
    }

    [Fact]
    public void Create_WithValidData_RaisesStockReservationCreatedEvent()
    {
        var reservation = StockReservation.Create(SomeOrder, SomeItems, Guid.NewGuid());

        Assert.Single(reservation.UncommittedEvents);
        Assert.IsType<StockReservationCreated>(reservation.UncommittedEvents[0]);
    }

    [Fact]
    public void Create_AssignsNonEmptyId()
    {
        var reservation = StockReservation.Create(SomeOrder, SomeItems, Guid.NewGuid());

        Assert.NotEqual(Guid.Empty, reservation.Id);
    }

    [Fact]
    public void Create_SetsExpiresAtTwoHoursFromNow()
    {
        var before = DateTimeOffset.UtcNow;
        var reservation = StockReservation.Create(SomeOrder, SomeItems, Guid.NewGuid());
        var after = DateTimeOffset.UtcNow;

        Assert.True(reservation.ExpiresAt >= before.AddHours(2));
        Assert.True(reservation.ExpiresAt <= after.AddHours(2).AddSeconds(1));
    }

    [Fact]
    public void Fail_SetsStatusFailed()
    {
        var reservation = StockReservation.Fail(SomeOrder, SomeItems, SomeProduct, 10, 3, Guid.NewGuid());

        Assert.Equal(ReservationStatus.Failed, reservation.Status);
    }

    [Fact]
    public void Fail_RaisesStockReservationFailedEvent()
    {
        var reservation = StockReservation.Fail(SomeOrder, SomeItems, SomeProduct, 10, 3, Guid.NewGuid());

        Assert.Single(reservation.UncommittedEvents);
        Assert.IsType<StockReservationFailed>(reservation.UncommittedEvents[0]);
    }

    [Fact]
    public void Release_FromReservedState_SetsStatusReleased()
    {
        var reservation = CreateReservedReservation();

        reservation.Release(Guid.NewGuid());

        Assert.Equal(ReservationStatus.Released, reservation.Status);
    }

    [Fact]
    public void Release_FromReservedState_RaisesStockReservationReleasedEvent()
    {
        var reservation = CreateReservedReservation();

        reservation.Release(Guid.NewGuid());

        Assert.Contains(reservation.UncommittedEvents, e => e is StockReservationReleased);
    }

    [Fact]
    public void Release_FromReleasedState_ThrowsStockReservationException()
    {
        var reservation = CreateReservedReservation();
        reservation.Release(Guid.NewGuid());
        reservation.ClearUncommittedEvents();

        Assert.Throws<StockReservationException>(() => reservation.Release(Guid.NewGuid()));
    }

    [Fact]
    public void Release_FromFailedState_ThrowsStockReservationException()
    {
        var reservation = StockReservation.Fail(SomeOrder, SomeItems, SomeProduct, 10, 0, Guid.NewGuid());

        Assert.Throws<StockReservationException>(() => reservation.Release(Guid.NewGuid()));
    }

    [Fact]
    public void Expire_FromReservedState_SetsStatusExpired()
    {
        var reservation = CreateReservedReservation();

        reservation.Expire(Guid.NewGuid());

        Assert.Equal(ReservationStatus.Expired, reservation.Status);
    }

    [Fact]
    public void Expire_FromReleasedState_ThrowsStockReservationException()
    {
        var reservation = CreateReservedReservation();
        reservation.Release(Guid.NewGuid());
        reservation.ClearUncommittedEvents();

        Assert.Throws<StockReservationException>(() => reservation.Expire(Guid.NewGuid()));
    }

    [Fact]
    public void Rehydrate_FromCreatedAndReleasedEvents_ReconstructsReleasedState()
    {
        var original = StockReservation.Create(SomeOrder, SomeItems, Guid.NewGuid());
        original.Release(Guid.NewGuid());
        var events = original.UncommittedEvents;

        var rehydrated = StockReservation.Rehydrate(events);

        Assert.Equal(original.Id, rehydrated.Id);
        Assert.Equal(ReservationStatus.Released, rehydrated.Status);
    }

    [Fact]
    public void Rehydrate_FromEmptyEventList_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            StockReservation.Rehydrate(Array.Empty<ECommerceOrderProcessing.Shared.Domain.DomainEvent>()));
    }

    [Fact]
    public void ClearUncommittedEvents_RemovesAllEvents()
    {
        var reservation = StockReservation.Create(SomeOrder, SomeItems, Guid.NewGuid());

        reservation.ClearUncommittedEvents();

        Assert.Empty(reservation.UncommittedEvents);
    }

    private static StockReservation CreateReservedReservation()
    {
        var reservation = StockReservation.Create(SomeOrder, SomeItems, Guid.NewGuid());
        reservation.ClearUncommittedEvents();
        return reservation;
    }
}
