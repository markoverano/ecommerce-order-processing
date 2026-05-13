using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;
using Polly.Wrap;
using Prometheus;

namespace ECommerceOrderProcessing.Infrastructure.Resilience;

/// <summary>
/// Named Polly policies registered in DI for Payment and Shipping external API calls.
/// Retry: 3 attempts, exponential backoff (100ms * 2^n).
/// Circuit breaker: open after 5 failures in a 30s window, 30s break, then HALF-OPEN probe.
/// Timeout: 10s per individual call.
/// </summary>
public static class PollyPolicies
{
    public const string ExternalApiPolicyKey = "ExternalApi";
    public const string RetryOnlyPolicyKey = "RetryOnly";

    // 0 = Closed (healthy), 1 = Open (fast-failing), 2 = HalfOpen (probing)
    private static readonly Gauge CircuitBreakerState = Metrics
        .CreateGauge(
            "circuit_breaker_state",
            "Polly circuit breaker state: 0=Closed, 1=Open, 2=HalfOpen",
            new GaugeConfiguration { LabelNames = ["service"] });

    public static void RegisterPolicies(IPolicyRegistry<string> registry, ILogger logger, string serviceName = "unknown")
    {
        var timeout = Policy.TimeoutAsync(
            seconds: 10,
            timeoutStrategy: TimeoutStrategy.Optimistic);

        var retry = Policy
            .Handle<Exception>(ex => ex is not BrokenCircuitException and not TimeoutRejectedException)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)),
                onRetry: (exception, delay, attempt, _) =>
                    logger.LogWarning(exception, "Retry {Attempt} after {Delay}ms", attempt, delay.TotalMilliseconds));

        var circuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (ex, duration) =>
                {
                    logger.LogError(ex, "Circuit breaker OPEN for {Duration}s", duration.TotalSeconds);
                    CircuitBreakerState.WithLabels(serviceName).Set(1);
                },
                onReset: () =>
                {
                    logger.LogInformation("Circuit breaker CLOSED");
                    CircuitBreakerState.WithLabels(serviceName).Set(0);
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("Circuit breaker HALF-OPEN");
                    CircuitBreakerState.WithLabels(serviceName).Set(2);
                });

        AsyncPolicyWrap externalApi = Policy.WrapAsync(retry, circuitBreaker, timeout);
        registry.Add(ExternalApiPolicyKey, externalApi);
        registry.Add(RetryOnlyPolicyKey, retry);
    }
}
