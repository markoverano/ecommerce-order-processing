using System.Net.Http.Json;
using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.Resilience;
using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;
using ShippingService.Application.ExternalClients;
using ShippingService.Domain.Exceptions;

namespace ShippingService.Infrastructure.ExternalClients;

public sealed class FedExShippingClient : IFedExShippingClient
{
    private readonly HttpClient _httpClient;
    private readonly IAsyncPolicy _policy;
    private readonly ILogger<FedExShippingClient> _logger;

    public FedExShippingClient(
        HttpClient httpClient,
        IConfiguration configuration,
        IReadOnlyPolicyRegistry<string> registry,
        ILogger<FedExShippingClient> logger)
    {
        _httpClient = httpClient;
        _policy = registry.Get<IAsyncPolicy>(PollyPolicies.ExternalApiPolicyKey);
        _logger = logger;

        var apiKey = configuration["FedEx__ApiKey"]
            ?? throw new InvalidOperationException("FedEx__ApiKey is not configured.");
        _httpClient.DefaultRequestHeaders.Add("X-FedEx-Api-Key", apiKey);
    }

    public async Task<FedExShipmentResult> CreateShipmentAsync(
        OrderId orderId,
        ShippingAddress destination,
        IReadOnlyList<ShipmentItem> items,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            idempotencyKey = idempotencyKey.ToString(),
            orderId = orderId.Value.ToString(),
            recipient = new
            {
                address = new
                {
                    streetLines = new[] { destination.Line1, destination.Line2 }.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray(),
                    city = destination.City,
                    stateOrProvinceCode = destination.State,
                    postalCode = destination.PostalCode,
                    countryCode = destination.CountryCode
                }
            },
            packages = items.Select(i => new { description = i.Description, quantity = i.Quantity }).ToArray()
        };

        try
        {
            var response = await _policy.ExecuteAsync(async ct =>
            {
                var httpResponse = await _httpClient.PostAsJsonAsync("/v1/ship/create", payload, ct);
                httpResponse.EnsureSuccessStatusCode();
                return httpResponse;
            }, cancellationToken);

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = doc.RootElement;

            var trackingNumber = root.TryGetProperty("trackingNumber", out var tn) ? tn.GetString() : null;

            if (trackingNumber is null)
                return new FedExShipmentResult(false, null, "FedEx did not return a tracking number.");

            return new FedExShipmentResult(true, trackingNumber, null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "FedEx API returned an error for order {OrderId}", orderId);
            throw new ShipmentProcessingException($"FedEx API error: {ex.Message}", ex);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "FedEx circuit breaker is open");
            throw new ShipmentProcessingException("Shipping service is temporarily unavailable. Please try again later.", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "FedEx API call timed out for order {OrderId}", orderId);
            throw new ShipmentProcessingException("Shipment request timed out.", ex);
        }
    }

    public async Task<FedExCancelResult> CancelShipmentAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _policy.ExecuteAsync(async ct =>
            {
                var httpResponse = await _httpClient.DeleteAsync($"/v1/ship/{trackingNumber}", ct);
                httpResponse.EnsureSuccessStatusCode();
                return httpResponse;
            }, cancellationToken);

            return new FedExCancelResult(true, null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "FedEx cancel request failed for tracking number {TrackingNumber}", trackingNumber);
            return new FedExCancelResult(false, $"FedEx API error: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "FedEx circuit breaker is open during cancel for tracking {TrackingNumber}", trackingNumber);
            throw new ShipmentProcessingException("Shipping service is temporarily unavailable.", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "FedEx cancel request timed out for tracking {TrackingNumber}", trackingNumber);
            throw new ShipmentProcessingException("Cancellation request timed out.", ex);
        }
    }
}
