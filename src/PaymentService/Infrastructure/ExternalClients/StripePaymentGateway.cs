using ECommerceOrderProcessing.Infrastructure.Resilience;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PaymentService.Application.ExternalClients;
using PaymentService.Domain.Exceptions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;
using Stripe;

namespace PaymentService.Infrastructure.ExternalClients;

public sealed class StripePaymentClient : IStripePaymentClient
{
    private readonly ChargeService _chargeService;
    private readonly RefundService _refundService;
    private readonly IAsyncPolicy _policy;
    private readonly ILogger<StripePaymentClient> _logger;

    public StripePaymentClient(
        IConfiguration configuration,
        IReadOnlyPolicyRegistry<string> registry,
        ILogger<StripePaymentClient> logger)
    {
        var apiKey = configuration["Stripe__ApiKey"]
            ?? throw new InvalidOperationException("Stripe__ApiKey is not configured.");

        // Stripe__BaseUrl is non-null only in test environments where WireMock stubs the Stripe API.
        var apiBase = configuration["Stripe__BaseUrl"];
        var client = string.IsNullOrEmpty(apiBase)
            ? new StripeClient(apiKey)
            : new StripeClient(apiKey, apiBase: apiBase);
        _chargeService = new ChargeService(client);
        _refundService = new RefundService(client);
        _policy = registry.Get<IAsyncPolicy>(PollyPolicies.ExternalApiPolicyKey);
        _logger = logger;
    }

    public async Task<StripeChargeResult> ChargeAsync(
        string paymentMethodId,
        Money amount,
        Guid idempotencyKey,
        OrderId orderId,
        CustomerId customerId,
        CancellationToken cancellationToken = default)
    {
        var options = new ChargeCreateOptions
        {
            Amount = (long)(amount.Amount * 100),
            Currency = amount.Currency.ToLowerInvariant(),
            Source = paymentMethodId,
            Description = $"Order payment ref:{idempotencyKey}",
            Metadata = new Dictionary<string, string>
            {
                ["order_id"] = orderId.Value.ToString(),
                ["customer_id"] = customerId.Value.ToString(),
                ["order_amount_cents"] = ((long)(amount.Amount * 100)).ToString()
            }
        };

        var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey.ToString() };

        try
        {
            var charge = await _policy.ExecuteAsync(ct =>
                _chargeService.CreateAsync(options, requestOptions, ct), cancellationToken);

            if (charge.Status == "succeeded")
                return new StripeChargeResult(true, charge.Id, null);

            _logger.LogWarning("Stripe charge returned non-succeeded status {Status} for idempotency key {Key}", charge.Status, idempotencyKey);
            return new StripeChargeResult(false, null, charge.FailureMessage ?? "Charge was not completed.");
        }
        catch (StripeException ex) when (IsNonRetriable(ex))
        {
            _logger.LogWarning(ex, "Stripe charge declined for key {Key}: {Code}", idempotencyKey, ex.StripeError?.Code);
            throw new PaymentProcessingException(ex.StripeError?.Message ?? "Payment declined.", ex);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Stripe circuit breaker is open");
            throw new PaymentProcessingException("Payment service is temporarily unavailable. Please try again later.", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "Stripe API call timed out for key {Key}", idempotencyKey);
            throw new PaymentProcessingException("Payment request timed out.", ex);
        }
    }

    public async Task<StripeRefundResult> RefundAsync(
        string chargeId,
        Money amount,
        CancellationToken cancellationToken = default)
    {
        var options = new RefundCreateOptions
        {
            Charge = chargeId,
            Amount = (long)(amount.Amount * 100)
        };

        try
        {
            var refund = await _policy.ExecuteAsync(ct =>
                _refundService.CreateAsync(options, cancellationToken: ct), cancellationToken);

            return refund.Status == "succeeded"
                ? new StripeRefundResult(true, null)
                : new StripeRefundResult(false, $"Refund status: {refund.Status}");
        }
        catch (StripeException ex) when (IsNonRetriable(ex))
        {
            _logger.LogWarning(ex, "Stripe refund rejected for charge {ChargeId}: {Code}", chargeId, ex.StripeError?.Code);
            throw new PaymentProcessingException(ex.StripeError?.Message ?? "Refund failed.", ex);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Stripe circuit breaker is open during refund for charge {ChargeId}", chargeId);
            throw new PaymentProcessingException("Payment service is temporarily unavailable.", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "Stripe refund timed out for charge {ChargeId}", chargeId);
            throw new PaymentProcessingException("Refund request timed out.", ex);
        }
    }

    // Card errors and invalid request errors are not transient; Polly should not retry them.
    private static bool IsNonRetriable(StripeException ex) =>
        ex.StripeError?.Type is "card_error" or "invalid_request_error";
}
