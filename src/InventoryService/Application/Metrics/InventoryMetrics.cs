using Prometheus;

namespace InventoryService.Application.Metrics;

internal static class InventoryMetrics
{
    internal static readonly Counter StockReservationsSucceeded = Prometheus.Metrics
        .CreateCounter("stock_reservations_succeeded_total", "Total stock reservations fulfilled");

    internal static readonly Counter StockReservationsFailed = Prometheus.Metrics
        .CreateCounter("stock_reservations_failed_total", "Total stock reservation attempts that resulted in OutOfStock");

    internal static readonly Counter StockReleases = Prometheus.Metrics
        .CreateCounter("stock_releases_total", "Total stock reservations released (compensation path)");
}
