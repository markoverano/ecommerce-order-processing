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

public sealed class MailgunNotificationClient : IMailgunNotificationClient
{
    private readonly HttpClient _httpClient;
    private readonly IAsyncPolicy _policy;
    private readonly string _fromAddress;
    private readonly ILogger<MailgunNotificationClient> _logger;

    public MailgunNotificationClient(
        HttpClient httpClient,
        IConfiguration configuration,
        IReadOnlyPolicyRegistry<string> registry,
        ILogger<MailgunNotificationClient> logger)
    {
        _httpClient = httpClient;
        _policy = registry.Get<IAsyncPolicy>(PollyPolicies.ExternalApiPolicyKey);
        _logger = logger;

        var apiKey = configuration["Mailgun__ApiKey"]
            ?? throw new InvalidOperationException("Mailgun__ApiKey is not configured.");
        var domain = configuration["Mailgun__Domain"]
            ?? throw new InvalidOperationException("Mailgun__Domain is not configured.");
        _fromAddress = configuration["Mailgun__FromAddress"] ?? $"noreply@{domain}";

        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{apiKey}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.BaseAddress = new Uri($"https://api.mailgun.net/v3/{domain}/");
    }

    public async Task<MailgunSendResult> SendEmailAsync(
        string toAddress,
        string subject,
        string body,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("from", _fromAddress),
                new KeyValuePair<string, string>("to", toAddress),
                new KeyValuePair<string, string>("subject", subject),
                new KeyValuePair<string, string>("text", body),
                // Mailgun custom variable echoed back in webhook payload for delivery correlation.
                new KeyValuePair<string, string>("v:notification-id", notificationId.ToString())
            });

            var response = await _policy.ExecuteAsync(async ct =>
            {
                var httpResponse = await _httpClient.PostAsync("messages", formContent, ct);
                httpResponse.EnsureSuccessStatusCode();
                return httpResponse;
            }, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
            var messageId = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

            _logger.LogInformation("Mailgun accepted email to {ToAddress}, messageId={MessageId}", toAddress, messageId);
            return new MailgunSendResult(true, messageId, null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Mailgun API returned an error for recipient {ToAddress}", toAddress);
            throw new NotificationException($"Mailgun API error: {ex.Message}", ex);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Mailgun circuit breaker is open");
            throw new NotificationException("Email service is temporarily unavailable.", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "Mailgun API call timed out for recipient {ToAddress}", toAddress);
            throw new NotificationException("Email send request timed out.", ex);
        }
    }
}
