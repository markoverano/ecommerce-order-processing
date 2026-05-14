using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using ShippingService.Application.ExternalClients;
using ShippingService.Application.Metrics;
using ShippingService.Domain.Enums;
using ShippingService.Domain.Repositories;

namespace ShippingService.Application.Commands;

public sealed class CancelShipmentCommandHandler : IRequestHandler<CancelShipmentCommand, ServiceResponse<bool>>
{
    private readonly IShipmentRepository _repository;
    private readonly IFedExShippingClient _fedEx;
    private readonly ILogger<CancelShipmentCommandHandler> _logger;

    public CancelShipmentCommandHandler(
        IShipmentRepository repository,
        IFedExShippingClient fedEx,
        ILogger<CancelShipmentCommandHandler> logger)
    {
        _repository = repository;
        _fedEx = fedEx;
        _logger = logger;
    }

    public async Task<ServiceResponse<bool>> Handle(CancelShipmentCommand command, CancellationToken cancellationToken)
    {
        var shipment = await _repository.GetByIdAsync(command.ShipmentId, cancellationToken);

        if (shipment is null)
            return ServiceResponse<bool>.Failure("SHIPMENT_NOT_FOUND", $"Shipment {command.ShipmentId} was not found.");

        if (shipment.Status is ShipmentStatus.Cancelled or ShipmentStatus.Delivered)
            return ServiceResponse<bool>.Failure("INVALID_STATUS", $"Cannot cancel a shipment in {shipment.Status} status.");

        if (shipment.TrackingNumber is not null)
        {
            var result = await _fedEx.CancelShipmentAsync(shipment.TrackingNumber.Value.Value, cancellationToken);
            if (!result.IsSuccess)
                return ServiceResponse<bool>.Failure("CANCELLATION_FAILED", result.ErrorMessage ?? "FedEx could not cancel the shipment.");
        }

        shipment.Cancel(command.CorrelationId);
        await _repository.SaveAsync(shipment, cancellationToken);

        ShippingMetrics.ShipmentsCancelled.Inc();

        _logger.LogInformation(
            "Shipment {ShipmentId} for order {OrderId} cancelled. CorrelationId={CorrelationId}",
            command.ShipmentId, command.OrderId, command.CorrelationId);

        return ServiceResponse<bool>.Success(true);
    }
}
