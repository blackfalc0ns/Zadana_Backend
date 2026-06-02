using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Zadana.Api.Controllers;
using Zadana.Api.Security;
using Zadana.Infrastructure.Settings;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/webhooks/nabda")]
[Tags("Webhooks")]
public sealed class NabdaWebhooksController(
    IOptions<NabdaOtpSettings> settings,
    ILogger<NabdaWebhooksController> logger) : ApiControllerBase
{
    private const string NabdaSecretHeader = "X-Nabda-Webhook-Secret";
    private const string GenericSecretHeader = "X-Webhook-Secret";

    [AllowAnonymous]
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicyNames.PaymentCallbacks)]
    public IActionResult Health()
    {
        return Ok(new
        {
            provider = "Nabda",
            status = "ready"
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicyNames.PaymentCallbacks)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        if (!IsWebhookSecretValid())
        {
            logger.LogWarning("Rejected Nabda webhook because the provided secret is invalid.");
            return Unauthorized(new
            {
                processed = false,
                provider = "Nabda",
                message = "Webhook secret is invalid."
            });
        }

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(payload))
        {
            logger.LogInformation("Nabda webhook probe received with an empty payload.");
            return Ok(new
            {
                processed = false,
                provider = "Nabda",
                status = "empty"
            });
        }

        NabdaWebhookEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<NabdaWebhookEnvelope>(
                payload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Nabda webhook payload could not be parsed. PayloadBytes={PayloadBytes}", payload.Length);
            return BadRequest(new
            {
                processed = false,
                provider = "Nabda",
                message = "Webhook payload is not valid JSON."
            });
        }

        var eventName = envelope?.Event ?? "unknown";
        var status = envelope?.Payload?.Status ?? "unknown";
        var messageId = envelope?.Payload?.MessageId;
        var phone = MaskPhone(envelope?.Payload?.Phone);

        logger.LogInformation(
            "Nabda webhook received. Event={Event} Status={Status} MessageId={MessageId} Phone={Phone} PayloadBytes={PayloadBytes}",
            eventName,
            status,
            string.IsNullOrWhiteSpace(messageId) ? "unknown" : messageId,
            phone,
            payload.Length);

        return Ok(new
        {
            processed = true,
            provider = "Nabda",
            eventName,
            status
        });
    }

    private bool IsWebhookSecretValid()
    {
        var expected = settings.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        var provided = FirstNonEmpty(
            Request.Headers[NabdaSecretHeader].ToString(),
            Request.Headers[GenericSecretHeader].ToString(),
            Request.Query["secret"].ToString());

        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected.Trim());
        var providedBytes = Encoding.UTF8.GetBytes(provided.Trim());
        return expectedBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return "***";
        }

        var compact = new string(phone.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
        if (compact.Length <= 4)
        {
            return new string('*', compact.Length);
        }

        return string.Concat(new string('*', compact.Length - 4), compact.AsSpan(compact.Length - 4));
    }

    private sealed record NabdaWebhookEnvelope(
        [property: JsonPropertyName("instanceId")] string? InstanceId,
        [property: JsonPropertyName("event")] string? Event,
        [property: JsonPropertyName("payload")] NabdaWebhookPayload? Payload,
        [property: JsonPropertyName("timestamp")] DateTimeOffset? Timestamp);

    private sealed record NabdaWebhookPayload(
        [property: JsonPropertyName("messageId")] string? MessageId,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("phone")] string? Phone,
        [property: JsonPropertyName("message")] string? Message);
}
