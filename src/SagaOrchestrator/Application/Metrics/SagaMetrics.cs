using Prometheus;

namespace SagaOrchestrator.Application.Metrics;

internal static class SagaMetrics
{
    internal static readonly Counter SagasStarted = Prometheus.Metrics
        .CreateCounter("sagas_started_total", "Total order-processing sagas started");

    internal static readonly Counter SagasCompleted = Prometheus.Metrics
        .CreateCounter("sagas_completed_total", "Total sagas completed successfully (order confirmed)");

    internal static readonly Counter SagasCompensated = Prometheus.Metrics
        .CreateCounter("sagas_compensated_total", "Total sagas fully compensated (order failed or rolled back)", ["reason"]);
}
