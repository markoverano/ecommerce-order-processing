using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.Webhooks;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
[AllowAnonymous]
public sealed class WebhooksController : ControllerBase
{
    private readonly MailgunWebhookHandler _mailgunHandler;
    private readonly TwilioWebhookHandler _twilioHandler;
    private readonly string _mailgunSigningKey;
    private readonly string _twilioAuthToken;

    public WebhooksController(
        MailgunWebhookHandler mailgunHandler,
        TwilioWebhookHandler twilioHandler,
        IConfiguration configuration)
    {
        _mailgunHandler = mailgunHandler;
        _twilioHandler = twilioHandler;
        _mailgunSigningKey = configuration["Mailgun__WebhookSigningKey"]
            ?? throw new InvalidOperationException("Mailgun__WebhookSigningKey is not configured.");
        _twilioAuthToken = configuration["Twilio__AuthToken"]
            ?? throw new InvalidOperationException("Twilio__AuthToken is not configured.");
    }

    /// <summary>Receives Mailgun delivery webhook events. HMAC-SHA256 signature verified before processing.</summary>
    [HttpPost("mailgun")]
    public async Task<IActionResult> Mailgun(CancellationToken cancellationToken)
    {
        string payload;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            payload = await reader.ReadToEndAsync(cancellationToken);

        var (timestamp, token, signature) = ExtractMailgunSignatureFields(payload);
        if (timestamp is null || token is null || signature is null || !VerifyMailgunSignature(timestamp, token, signature, _mailgunSigningKey))
            return Unauthorized();

        var (webhookId, eventType, providerMessageId, eventTimestamp) = ParseMailgunPayload(payload);
        if (webhookId is null || eventType is null || providerMessageId is null)
            return BadRequest("Unrecognised Mailgun event structure.");

        var correlationId = HttpContext.Items["CorrelationId"] is Guid cid ? cid : Guid.NewGuid();

        await _mailgunHandler.HandleAsync(webhookId, eventType, providerMessageId, eventTimestamp, correlationId, cancellationToken);

        return Ok();
    }

    /// <summary>Receives Twilio SMS status callback events. HMAC-SHA1 signature verified before processing.</summary>
    [HttpPost("twilio")]
    public async Task<IActionResult> Twilio(CancellationToken cancellationToken)
    {
        var form = await Request.ReadFormAsync(cancellationToken);

        var requestUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}";
        if (!VerifyTwilioSignature(requestUrl, form, _twilioAuthToken, Request.Headers["X-Twilio-Signature"].FirstOrDefault() ?? string.Empty))
            return Unauthorized();

        var messageSid = form["MessageSid"].FirstOrDefault();
        var messageStatus = form["MessageStatus"].FirstOrDefault();

        if (messageSid is null || messageStatus is null)
            return BadRequest("Missing MessageSid or MessageStatus in Twilio callback.");

        var webhookId = $"twilio:{messageSid}:{messageStatus}";
        var correlationId = HttpContext.Items["CorrelationId"] is Guid cid ? cid : Guid.NewGuid();

        await _twilioHandler.HandleAsync(webhookId, messageStatus, messageSid, correlationId, cancellationToken);

        return Ok();
    }

    // Mailgun signs: HMAC-SHA256(signing_key, timestamp + token)
    private static bool VerifyMailgunSignature(string timestamp, string token, string signature, string signingKey)
    {
        var data = $"{timestamp}{token}";
        using var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(signingKey));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.ASCII.GetBytes(data)));
        return string.Equals(computed, signature, StringComparison.OrdinalIgnoreCase);
    }

    // Twilio signs: HMAC-SHA1(auth_token, url + sorted_params_concatenated)
    private static bool VerifyTwilioSignature(string url, IFormCollection form, string authToken, string signature)
    {
        var sortedParams = form.OrderBy(kv => kv.Key).Aggregate(
            new StringBuilder(url),
            (sb, kv) => sb.Append(kv.Key).Append(kv.Value.FirstOrDefault()),
            sb => sb.ToString());

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(authToken));
        var computed = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(sortedParams)));
        return string.Equals(computed, signature, StringComparison.Ordinal);
    }

    private static (string? Timestamp, string? Token, string? Signature) ExtractMailgunSignatureFields(string payload)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("signature", out var sig))
                return (null, null, null);

            var timestamp = sig.TryGetProperty("timestamp", out var ts) ? ts.GetString() : null;
            var token = sig.TryGetProperty("token", out var tk) ? tk.GetString() : null;
            var signature = sig.TryGetProperty("signature", out var sv) ? sv.GetString() : null;

            return (timestamp, token, signature);
        }
        catch
        {
            return (null, null, null);
        }
    }

    // Minimal JSON parsing; avoids coupling to Mailgun SDK types.
    private static (string? WebhookId, string? EventType, string? ProviderMessageId, DateTimeOffset? EventTimestamp) ParseMailgunPayload(string payload)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("event-data", out var eventData))
                return (null, null, null, null);

            var webhookId = eventData.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var eventType = eventData.TryGetProperty("event", out var evEl) ? evEl.GetString() : null;

            DateTimeOffset? eventTimestamp = null;
            if (eventData.TryGetProperty("timestamp", out var tsEl) && tsEl.TryGetDouble(out var epochSeconds))
                eventTimestamp = DateTimeOffset.FromUnixTimeSeconds((long)epochSeconds);

            string? providerMessageId = null;
            if (eventData.TryGetProperty("message", out var msg) && msg.TryGetProperty("headers", out var headers))
                providerMessageId = headers.TryGetProperty("message-id", out var mid) ? mid.GetString() : null;

            return (webhookId, eventType, providerMessageId, eventTimestamp);
        }
        catch
        {
            return (null, null, null, null);
        }
    }
}
