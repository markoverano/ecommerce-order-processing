namespace ECommerceOrderProcessing.Shared.Commands;

/// <summary>
/// Marker for commands that carry a client-assigned or correlation-based idempotency key.
/// Implemented by the GlobalIdempotencyBehavior to short-circuit duplicate deliveries.
/// </summary>
public interface IIdempotentCommand
{
    string GetIdempotencyKey();
}
