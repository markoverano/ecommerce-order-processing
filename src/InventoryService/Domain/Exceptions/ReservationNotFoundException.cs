using ECommerceOrderProcessing.Shared.ValueObjects;

namespace InventoryService.Domain.Exceptions;

public sealed class ReservationNotFoundException : Exception
{
    public ReservationNotFoundException(ReservationId reservationId)
        : base($"Reservation {reservationId} was not found.") { }
}
