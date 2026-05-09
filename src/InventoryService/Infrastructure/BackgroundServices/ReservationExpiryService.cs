using InventoryService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InventoryService.Infrastructure.BackgroundServices;

/// <summary>
/// Periodically scans for reservations past their 2-hour TTL, releases product quantities, and marks them expired.
/// </summary>
public sealed class ReservationExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationExpiryService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    public ReservationExpiryService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReservationExpiryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredReservationsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error processing expired reservations");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task ProcessExpiredReservationsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var reservationRepository = scope.ServiceProvider.GetRequiredService<IStockReservationRepository>();
        var productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();

        var expired = await reservationRepository.GetExpiredReservationsAsync(cancellationToken);
        if (expired.Count == 0)
            return;

        _logger.LogInformation("Expiring {Count} overdue reservations", expired.Count);

        foreach (var reservation in expired)
        {
            try
            {
                var productIds = reservation.Items.Select(i => i.ProductId).ToList();
                var products = await productRepository.GetByIdsAsync(productIds, cancellationToken);

                var modifiedProducts = new List<Domain.Aggregates.Product>();
                foreach (var item in reservation.Items)
                {
                    var product = products.FirstOrDefault(p => p.ProductId == item.ProductId);
                    if (product is null)
                    {
                        _logger.LogWarning(
                            "Product {ProductId} not found while expiring reservation {ReservationId}",
                            item.ProductId, reservation.ReservationId);
                        continue;
                    }
                    product.Release(item.Quantity);
                    modifiedProducts.Add(product);
                }

                reservation.Expire(Guid.NewGuid());
                await reservationRepository.SaveAsync(reservation, modifiedProducts, cancellationToken);

                _logger.LogInformation("Expired reservation {ReservationId}", reservation.ReservationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to expire reservation {ReservationId}", reservation.ReservationId);
            }
        }
    }
}
