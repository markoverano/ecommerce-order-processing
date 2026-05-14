using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using ShippingService.Application.ExternalClients;
using ShippingService.Application.Metrics;
using ShippingService.Application.Validation;
using ShippingService.Domain.Aggregates;
using ShippingService.Domain.Exceptions;
using ShippingService.Domain.Repositories;
using ShippingService.Domain.ValueObjects;

namespace ShippingService.Application.Commands;

public sealed class CreateShipmentCommandHandler : IRequestHandler<CreateShipmentCommand, ServiceResponse<ShipmentId>>
{
    private readonly IShipmentRepository _repository;
    private readonly IFedExShippingClient _fedEx;
    private readonly CreateShipmentCommandValidator _validator;
    private readonly ILogger<CreateShipmentCommandHandler> _logger;

    public CreateShipmentCommandHandler(
        IShipmentRepository repository,
        IFedExShippingClient fedEx,
        CreateShipmentCommandValidator validator,
        ILogger<CreateShipmentCommandHandler> logger)
    {
        _repository = repository;
        _fedEx = fedEx;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ServiceResponse<ShipmentId>> Handle(CreateShipmentCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return ServiceResponse<ShipmentId>.Failure("VALIDATION_FAILED", errors);
        }

        var shipment = Shipment.Create(command.OrderId, command.CustomerId, command.ShippingAddress, command.Items, command.CorrelationId);

        try
        {
            var result = await _fedEx.CreateShipmentAsync(
                command.OrderId,
                command.ShippingAddress,
                command.Items,
                command.CorrelationId,
                cancellationToken);

            if (result.IsSuccess)
                shipment.MarkAsCreated(TrackingNumber.From(result.TrackingNumber!), command.CorrelationId);
            else
                shipment.MarkAsFailed(result.ErrorMessage ?? "FedEx rejected the shipment request.", command.CorrelationId);
        }
        catch (ShipmentProcessingException ex)
        {
            _logger.LogWarning(ex, "FedEx shipment request failed for order {OrderId}: {Reason}", command.OrderId, ex.Message);
            shipment.MarkAsFailed(ex.Message, command.CorrelationId);
        }

        await _repository.SaveAsync(shipment, cancellationToken);

        if (shipment.Status == ShippingService.Domain.Enums.ShipmentStatus.Created)
            ShippingMetrics.ShipmentsCreated.Inc();
        else
            ShippingMetrics.ShipmentsFailed.Inc();

        _logger.LogInformation(
            "Shipment {ShipmentId} for order {OrderId} completed with status {Status}. CorrelationId={CorrelationId}",
            shipment.ShipmentId, command.OrderId, shipment.Status, command.CorrelationId);

        return ServiceResponse<ShipmentId>.Success(shipment.ShipmentId);
    }
}
