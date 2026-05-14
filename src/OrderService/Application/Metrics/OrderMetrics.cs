using Prometheus;

namespace OrderService.Application.Metrics;

internal static class OrderMetrics
{
    internal static readonly Counter OrdersCreated = Prometheus.Metrics
        .CreateCounter("orders_created_total", "Total orders successfully created");

    internal static readonly Counter OrderCreationFailed = Prometheus.Metrics
        .CreateCounter("order_creation_failed_total", "Total order creation attempts that failed validation");
}
