using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace ECommerceOrderProcessing.Infrastructure.Auth;

/// <summary>
/// Fetches a Keycloak client credentials token and attaches it to outgoing HTTP requests.
/// Token is cached until 30 seconds before its expiry so each downstream call is not preceded
/// by a token fetch — only the first call and subsequent calls after near-expiry pay the cost.
/// </summary>
public sealed class KeycloakClientCredentialsHandler : DelegatingHandler
{
    private readonly ClientCredentialsSettings _settings;
    private readonly ILogger<KeycloakClientCredentialsHandler> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    public KeycloakClientCredentialsHandler(
        ClientCredentialsSettings settings,
        ILogger<KeycloakClientCredentialsHandler> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await GetOrRefreshTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetOrRefreshTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _cachedToken;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
                return _cachedToken;

            // A dedicated HttpClient is intentional here: using the inner handler would cause
            // infinite recursion since this handler wraps it.
            using var tokenClient = new HttpClient();
            var response = await tokenClient.PostAsync(
                _settings.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _settings.ClientId,
                    ["client_secret"] = _settings.ClientSecret
                }),
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Keycloak returned an empty token response.");

            _cachedToken = payload.AccessToken;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn - 30);

            _logger.LogDebug("Refreshed client credentials token for {ClientId}, valid for {ExpiresIn}s", _settings.ClientId, payload.ExpiresIn);

            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed record KeycloakTokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn);
}
