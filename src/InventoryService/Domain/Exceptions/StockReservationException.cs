namespace InventoryService.Domain.Exceptions;

public sealed class StockReservationException : Exception
{
    public StockReservationException(string message) : base(message) { }
}
