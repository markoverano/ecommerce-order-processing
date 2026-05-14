using Prometheus;

namespace PaymentService.Application.Metrics;

internal static class PaymentMetrics
{
    internal static readonly Counter PaymentsProcessed = Prometheus.Metrics
        .CreateCounter("payments_processed_total", "Total payments successfully processed by Stripe");

    internal static readonly Counter PaymentsFailed = Prometheus.Metrics
        .CreateCounter("payments_failed_total", "Total payment attempts rejected or declined");

    internal static readonly Counter PaymentsRefunded = Prometheus.Metrics
        .CreateCounter("payments_refunded_total", "Total payments successfully refunded");
}
