using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ShippingService.Application.ExternalClients;

public sealed record FedExShipmentResult(bool IsSuccess, string? TrackingNumber, string? ErrorMessage);
public sealed record FedExCancelResult(bool IsSuccess, string? ErrorMessage);

public interface IFedExShippingClient
{
    Task<FedExShipmentResult> CreateShipmentAsync(
        OrderId orderId,
        ShippingAddress destination,
        IReadOnlyList<ShipmentItem> items,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<FedExCancelResult> CancelShipmentAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default);
}
