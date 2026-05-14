using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Models;
using InventoryService.Application.Metrics;
using InventoryService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InventoryService.Application.Commands;

public sealed class ReleaseStockCommandHandler : IRequestHandler<ReleaseStockCommand, ServiceResponse<bool>>
{
    private readonly IStockReservationRepository _reservationRepository;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<ReleaseStockCommandHandler> _logger;

    public ReleaseStockCommandHandler(
        IStockReservationRepository reservationRepository,
        IProductRepository productRepository,
        ILogger<ReleaseStockCommandHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<ServiceResponse<bool>> Handle(ReleaseStockCommand command, CancellationToken cancellationToken)
    {
        var reservation = await _reservationRepository.GetByIdAsync(command.ReservationId, cancellationToken);
        if (reservation is null)
            return ServiceResponse<bool>.Failure("RESERVATION_NOT_FOUND", $"Reservation {command.ReservationId} was not found.");

        var productIds = reservation.Items.Select(i => i.ProductId).ToList();
        var products = await _productRepository.GetByIdsAsync(productIds, cancellationToken);

        var modifiedProducts = new List<Domain.Aggregates.Product>();
        foreach (var item in reservation.Items)
        {
            var product = products.FirstOrDefault(p => p.ProductId == item.ProductId);
            if (product is null)
            {
                _logger.LogError("Product {ProductId} not found during release of reservation {ReservationId}", item.ProductId, command.ReservationId);
                continue;
            }
            product.Release(item.Quantity);
            modifiedProducts.Add(product);
        }

        reservation.Release(command.CorrelationId);
        await _reservationRepository.SaveAsync(reservation, modifiedProducts, cancellationToken);

        InventoryMetrics.StockReleases.Inc();

        _logger.LogInformation(
            "Released reservation {ReservationId} for order {OrderId}. CorrelationId={CorrelationId}",
            command.ReservationId, command.OrderId, command.CorrelationId);

        return ServiceResponse<bool>.Success(true);
    }
}
