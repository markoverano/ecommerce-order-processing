using Prometheus;

namespace ShippingService.Application.Metrics;

internal static class ShippingMetrics
{
    internal static readonly Counter ShipmentsCreated = Prometheus.Metrics
        .CreateCounter("shipments_created_total", "Total shipments successfully created with FedEx");

    internal static readonly Counter ShipmentsFailed = Prometheus.Metrics
        .CreateCounter("shipments_failed_total", "Total shipment creation attempts that failed");

    internal static readonly Counter ShipmentsCancelled = Prometheus.Metrics
        .CreateCounter("shipments_cancelled_total", "Total shipments cancelled during saga compensation");
}
