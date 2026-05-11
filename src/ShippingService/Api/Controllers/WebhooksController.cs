using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using ShippingService.Application.Webhooks;

namespace ShippingService.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
public sealed class WebhooksController : ControllerBase
{
    private readonly FedExWebhookHandler _webhookHandler;
    private readonly string _webhookSecret;

    public WebhooksController(FedExWebhookHandler webhookHandler, IConfiguration configuration)
    {
        _webhookHandler = webhookHandler;
        _webhookSecret = configuration["FedEx__WebhookSecret"]
            ?? throw new InvalidOperationException("FedEx__WebhookSecret is not configured.");
    }

    /// <summary>Receives FedEx webhook events. HMAC-SHA256 signature verified before processing.</summary>
    [HttpPost("fedex")]
    public async Task<IActionResult> FedEx(CancellationToken cancellationToken)
    {
        string payload;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            payload = await reader.ReadToEndAsync(cancellationToken);

        var signature = Request.Headers["X-FedEx-Signature"].FirstOrDefault();
        if (signature is null || !VerifyFedExSignature(payload, signature, _webhookSecret))
            return Unauthorized();

        var (webhookId, eventType, trackingNumber, eventTimestamp) = ParseFedExPayload(payload);
        if (webhookId is null || eventType is null || trackingNumber is null)
            return BadRequest("Unrecognised FedEx event structure.");

        var correlationId = HttpContext.Items["CorrelationId"] is Guid cid ? cid : Guid.NewGuid();

        await _webhookHandler.HandleAsync(webhookId, eventType, trackingNumber, eventTimestamp, correlationId, cancellationToken);

        return Ok();
    }

    private static bool VerifyFedExSignature(string payload, string signature, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        return string.Equals(computed, signature, StringComparison.OrdinalIgnoreCase);
    }

    // Minimal JSON parsing to avoid coupling the API layer to FedEx SDK types.
    private static (string? Id, string? EventType, string? TrackingNumber, DateTimeOffset? EventTimestamp) ParseFedExPayload(string payload)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var id = root.TryGetProperty("eventId", out var idEl) ? idEl.GetString() : null;
            var eventType = root.TryGetProperty("eventType", out var typeEl) ? typeEl.GetString() : null;
            var trackingNumber = root.TryGetProperty("trackingNumber", out var tnEl) ? tnEl.GetString() : null;

            DateTimeOffset? eventTimestamp = null;
            if (root.TryGetProperty("eventTimestamp", out var tsEl) && tsEl.TryGetDateTimeOffset(out var ts))
                eventTimestamp = ts;

            return (id, eventType, trackingNumber, eventTimestamp);
        }
        catch
        {
            return (null, null, null, null);
        }
    }
}
