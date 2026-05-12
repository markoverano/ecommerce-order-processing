using System.Net.Http.Headers;
using System.Text;
using ECommerceOrderProcessing.Infrastructure.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationService.Application.ExternalClients;
using NotificationService.Domain.Exceptions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;

namespace NotificationService.Infrastructure.ExternalClients;

public sealed class TwilioNotificationClient : ITwilioNotificationClient
{
    private readonly HttpClient _httpClient;
    private readonly IAsyncPolicy _policy;
    private readonly string _fromPhoneNumber;
    private readonly ILogger<TwilioNotificationClient> _logger;

    public TwilioNotificationClient(
        HttpClient httpClient,
        IConfiguration configuration,
        IReadOnlyPolicyRegistry<string> registry,
        ILogger<TwilioNotificationClient> logger)
    {
        _httpClient = httpClient;
        _policy = registry.Get<IAsyncPolicy>(PollyPolicies.ExternalApiPolicyKey);
        _logger = logger;

        var accountSid = configuration["Twilio__AccountSid"]
            ?? throw new InvalidOperationException("Twilio__AccountSid is not configured.");
        var authToken = configuration["Twilio__AuthToken"]
            ?? throw new InvalidOperationException("Twilio__AuthToken is not configured.");
        _fromPhoneNumber = configuration["Twilio__FromPhoneNumber"]
            ?? throw new InvalidOperationException("Twilio__FromPhoneNumber is not configured.");

        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.BaseAddress = new Uri($"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/");
    }

    public async Task<TwilioSendResult> SendSmsAsync(
        string toPhoneNumber,
        string message,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("From", _fromPhoneNumber),
                new KeyValuePair<string, string>("To", toPhoneNumber),
                new KeyValuePair<string, string>("Body", message)
            });

            var response = await _policy.ExecuteAsync(async ct =>
            {
                var httpResponse = await _httpClient.PostAsync("Messages.json", formContent, ct);
                httpResponse.EnsureSuccessStatusCode();
                return httpResponse;
            }, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
            var sid = doc.RootElement.TryGetProperty("sid", out var sidEl) ? sidEl.GetString() : null;

            _logger.LogInformation("Twilio accepted SMS to {ToPhoneNumber}, sid={Sid}", toPhoneNumber, sid);
            return new TwilioSendResult(true, sid, null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Twilio API returned an error for recipient {ToPhoneNumber}", toPhoneNumber);
            throw new NotificationException($"Twilio API error: {ex.Message}", ex);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Twilio circuit breaker is open");
            throw new NotificationException("SMS service is temporarily unavailable.", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "Twilio API call timed out for recipient {ToPhoneNumber}", toPhoneNumber);
            throw new NotificationException("SMS send request timed out.", ex);
        }
    }
}
