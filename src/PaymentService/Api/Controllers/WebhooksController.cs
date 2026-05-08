using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PaymentService.Application.Webhooks;

namespace PaymentService.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
public sealed class WebhooksController : ControllerBase
{
    private readonly StripeWebhookHandler _webhookHandler;
    private readonly string _webhookSecret;

    public WebhooksController(StripeWebhookHandler webhookHandler, IConfiguration configuration)
    {
        _webhookHandler = webhookHandler;
        _webhookSecret = configuration["Stripe__WebhookSecret"]
            ?? throw new InvalidOperationException("Stripe__WebhookSecret is not configured.");
    }

    /// <summary>Receives Stripe webhook events. HMAC signature verified before processing.</summary>
    [HttpPost("stripe")]
    public async Task<IActionResult> Stripe(CancellationToken cancellationToken)
    {
        string payload;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            payload = await reader.ReadToEndAsync(cancellationToken);

        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();
        if (signature is null || !VerifyStripeSignature(payload, signature, _webhookSecret))
        {
            return Unauthorized();
        }

        var (webhookId, eventType, chargeId, failureMessage) = ParseStripePayload(payload);
        if (webhookId is null || eventType is null || chargeId is null)
            return BadRequest("Unrecognised Stripe event structure.");

        var correlationId = HttpContext.Items["CorrelationId"] is Guid cid ? cid : Guid.NewGuid();

        await _webhookHandler.HandleAsync(webhookId, eventType, chargeId, failureMessage, correlationId, cancellationToken);

        return Ok();
    }

    private static bool VerifyStripeSignature(string payload, string signature, string secret)
    {
        // Stripe-Signature: t=TIMESTAMP,v1=HMAC_SHA256,...
        var parts = signature.Split(',');
        var timestamp = parts.FirstOrDefault(p => p.StartsWith("t=", StringComparison.Ordinal))?.Substring(2);
        var hash = parts.FirstOrDefault(p => p.StartsWith("v1=", StringComparison.Ordinal))?.Substring(3);

        if (timestamp is null || hash is null)
            return false;

        // Stripe signed payload format: "{timestamp}.{body}"
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload)));

        return string.Equals(computed, hash, StringComparison.OrdinalIgnoreCase);
    }

    // Minimal JSON parsing to avoid taking a Stripe SDK dependency in the API project.
    private static (string? Id, string? Type, string? ChargeId, string? FailureMessage) ParseStripePayload(string payload)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;

            string? chargeId = null;
            string? failureMessage = null;

            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("object", out var obj))
            {
                chargeId = obj.TryGetProperty("id", out var chEl) ? chEl.GetString() : null;
                failureMessage = obj.TryGetProperty("failure_message", out var fmEl) ? fmEl.GetString() : null;
            }

            return (id, type, chargeId, failureMessage);
        }
        catch
        {
            return (null, null, null, null);
        }
    }
}
