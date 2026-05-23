using System.Diagnostics;
using System.Text;

namespace ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;

/// <summary>
/// Extracts W3C traceparent/tracestate from RabbitMQ message headers so consumers
/// can create child spans linked to the publisher's trace.
/// </summary>
internal static class TraceContextPropagator
{
    internal static ActivityContext Extract(IDictionary<string, object>? headers)
    {
        if (headers is null || !headers.TryGetValue("traceparent", out var traceparentObj))
            return default;

        var traceparent = traceparentObj is byte[] bytes
            ? Encoding.UTF8.GetString(bytes)
            : traceparentObj?.ToString();

        if (string.IsNullOrEmpty(traceparent))
            return default;

        string? tracestate = null;
        if (headers.TryGetValue("tracestate", out var tracestateObj))
        {
            tracestate = tracestateObj is byte[] tsBytes
                ? Encoding.UTF8.GetString(tsBytes)
                : tracestateObj?.ToString();
        }

        return ActivityContext.TryParse(traceparent, tracestate, isRemote: true, out var context)
            ? context
            : default;
    }
}
