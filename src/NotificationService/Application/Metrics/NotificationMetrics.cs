using Prometheus;

namespace NotificationService.Application.Metrics;

internal static class NotificationMetrics
{
    internal static readonly Counter NotificationsSent = Prometheus.Metrics
        .CreateCounter("notifications_sent_total", "Total customer notifications sent", ["channel"]);

    internal static readonly Counter NotificationsFailed = Prometheus.Metrics
        .CreateCounter("notifications_failed_total", "Total customer notification attempts that failed", ["channel"]);
}
