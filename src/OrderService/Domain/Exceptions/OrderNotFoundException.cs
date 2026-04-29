namespace OrderService.Domain.Exceptions;

public sealed class OrderNotFoundException : KeyNotFoundException
{
    public OrderNotFoundException(Guid orderId)
        : base($"Order {orderId} was not found.") { }
}
