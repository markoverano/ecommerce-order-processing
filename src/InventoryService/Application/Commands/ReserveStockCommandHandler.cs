using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Application.Metrics;
using InventoryService.Application.Validation;
using InventoryService.Domain.Aggregates;
using InventoryService.Domain.Exceptions;
using InventoryService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InventoryService.Application.Commands;

public sealed class ReserveStockCommandHandler : IRequestHandler<ReserveStockCommand, ServiceResponse<ReservationId>>
{
    private readonly IStockReservationRepository _reservationRepository;
    private readonly IProductRepository _productRepository;
    private readonly ReserveStockCommandValidator _validator;
    private readonly ILogger<ReserveStockCommandHandler> _logger;

    public ReserveStockCommandHandler(
        IStockReservationRepository reservationRepository,
        IProductRepository productRepository,
        ReserveStockCommandValidator validator,
        ILogger<ReserveStockCommandHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _productRepository = productRepository;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ServiceResponse<ReservationId>> Handle(ReserveStockCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return ServiceResponse<ReservationId>.Failure("VALIDATION_FAILED", errors);
        }

        var productIds = command.Items.Select(i => i.ProductId).ToList();
        var products = await _productRepository.GetByIdsAsync(productIds, cancellationToken);

        // Validate all products exist before attempting any reservation.
        foreach (var item in command.Items)
        {
            if (!products.Any(p => p.ProductId == item.ProductId))
                return ServiceResponse<ReservationId>.Failure("PRODUCT_NOT_FOUND", $"Product {item.ProductId} was not found.");
        }

        // Find the first item that cannot be satisfied.
        StockReservationItem? unavailableItem = null;
        int availableQty = 0;
        foreach (var item in command.Items)
        {
            var product = products.First(p => p.ProductId == item.ProductId);
            if (product.AvailableQuantity < item.Quantity)
            {
                unavailableItem = item;
                availableQty = product.AvailableQuantity;
                break;
            }
        }

        if (unavailableItem is not null)
        {
            var failedReservation = StockReservation.Fail(
                command.OrderId,
                command.Items,
                unavailableItem.ProductId,
                unavailableItem.Quantity,
                availableQty,
                command.CorrelationId);

            await _reservationRepository.SaveAsync(failedReservation, Array.Empty<Product>(), cancellationToken);

            InventoryMetrics.StockReservationsFailed.Inc();

            _logger.LogWarning(
                "Stock unavailable for order {OrderId}: product {ProductId} has {Available}, requested {Requested}. CorrelationId={CorrelationId}",
                command.OrderId, unavailableItem.ProductId, availableQty, unavailableItem.Quantity, command.CorrelationId);

            return ServiceResponse<ReservationId>.Failure("OUT_OF_STOCK",
                $"Insufficient stock for product {unavailableItem.ProductId}: requested {unavailableItem.Quantity}, available {availableQty}.");
        }

        // All items are available — reserve each product's quantity.
        var modifiedProducts = new List<Product>();
        foreach (var item in command.Items)
        {
            var product = products.First(p => p.ProductId == item.ProductId);
            product.TryReserve(item.Quantity);
            modifiedProducts.Add(product);
        }

        var reservation = StockReservation.Create(command.OrderId, command.Items, command.CorrelationId);
        await _reservationRepository.SaveAsync(reservation, modifiedProducts, cancellationToken);

        InventoryMetrics.StockReservationsSucceeded.Inc();

        _logger.LogInformation(
            "Reserved stock for order {OrderId}, reservation {ReservationId}. CorrelationId={CorrelationId}",
            command.OrderId, reservation.ReservationId, command.CorrelationId);

        return ServiceResponse<ReservationId>.Success(reservation.ReservationId);
    }
}
