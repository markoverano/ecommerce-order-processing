using ECommerceOrderProcessing.Infrastructure.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Xunit;

namespace PaymentService.Application.Tests;

public sealed class CircuitBreakerPolicyTests
{
    private static IReadOnlyPolicyRegistry<string> BuildRegistry()
    {
        var registry = new PolicyRegistry();
        PollyPolicies.RegisterPolicies(registry, NullLogger.Instance);
        return registry;
    }

    [Fact]
    public void ExternalApiPolicy_IsRegistered()
    {
        var registry = BuildRegistry();

        Assert.True(registry.ContainsKey(PollyPolicies.ExternalApiPolicyKey));
    }

    [Fact]
    public void RetryOnlyPolicy_IsRegistered()
    {
        var registry = BuildRegistry();

        Assert.True(registry.ContainsKey(PollyPolicies.RetryOnlyPolicyKey));
    }

    [Fact]
    public async Task ExternalApiPolicy_OnTransientFailure_RetriesBeforeThrowing()
    {
        var registry = BuildRegistry();
        var policy = registry.Get<IAsyncPolicy>(PollyPolicies.ExternalApiPolicyKey);

        var callCount = 0;

        // Throw on every attempt; expect 3 retries (4 total attempts) then the exception surfaces.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await policy.ExecuteAsync(() =>
            {
                callCount++;
                throw new InvalidOperationException("transient");
#pragma warning disable CS0162
                return Task.CompletedTask;
#pragma warning restore CS0162
            }));

        // Initial call + 3 retries
        Assert.Equal(4, callCount);
    }

    [Fact]
    public async Task ExternalApiPolicy_AfterFiveConsecutiveFailures_CircuitBreaksOpen()
    {
        var registry = BuildRegistry();
        var policy = registry.Get<IAsyncPolicy>(PollyPolicies.ExternalApiPolicyKey);

        // Exhaust the circuit breaker threshold (5 failures required to open).
        // Each attempt fires initial + 3 retries = 4 calls per iteration, but we only need 5 exceptions
        // at the circuit-breaker level. We drive them in tight loops ignoring BrokenCircuitException.
        for (var i = 0; i < 5; i++)
        {
            try { await policy.ExecuteAsync(() => throw new Exception("failure")); }
            catch (BrokenCircuitException) { break; }
            catch { /* expected non-CB exception */ }
        }

        // Circuit should now be open; next call must fast-fail with BrokenCircuitException.
        await Assert.ThrowsAsync<BrokenCircuitException>(async () =>
            await policy.ExecuteAsync(() => Task.CompletedTask));
    }

    [Fact]
    public async Task ExternalApiPolicy_WhenCircuitOpen_FastFailsWithoutCallingDelegate()
    {
        var registry = BuildRegistry();
        var policy = registry.Get<IAsyncPolicy>(PollyPolicies.ExternalApiPolicyKey);

        // Open the circuit.
        for (var i = 0; i < 5; i++)
        {
            try { await policy.ExecuteAsync(() => throw new Exception("open circuit")); }
            catch { /* ignore */ }
        }

        var delegateCalled = false;
        var start = DateTimeOffset.UtcNow;

        try
        {
            await policy.ExecuteAsync(() =>
            {
                delegateCalled = true;
                return Task.CompletedTask;
            });
        }
        catch (BrokenCircuitException) { /* expected */ }

        Assert.False(delegateCalled);
        // Fast-fail should complete in well under 1 second.
        Assert.True((DateTimeOffset.UtcNow - start).TotalMilliseconds < 1000);
    }
}
