using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Zadana.Api.Controllers;
using Zadana.Api.Security;
using Zadana.Infrastructure.Settings;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/webhooks/wapilot")]
[Tags("Webhooks")]
public sealed class WapilotWebhooksController(
    IOptions<WapilotOtpSettings> settings,
    ILogger<WapilotWebhooksController> logger) : ApiControllerBase
{
    private const string WapilotSecretHeader = "X-Wapilot-Webhook-Secret";
    private const string GenericSecretHeader = "X-Webhook-Secret";

    [AllowAnonymous]
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicyNames.PaymentCallbacks)]
    public IActionResult Health()
    {
        return Ok(new
        {
            provider = "WAPIlot",
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
            logger.LogWarning("Rejected WAPIlot webhook because the provided secret is invalid.");
            return Unauthorized(new
            {
                processed = false,
                provider = "WAPIlot",
                message = "Webhook secret is invalid."
            });
        }

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(payload))
        {
            logger.LogInformation("WAPIlot webhook probe received with an empty payload.");
            return Ok(new
            {
                processed = false,
                provider = "WAPIlot",
                status = "empty"
            });
        }

        WapilotWebhookSummary summary;
        try
        {
            summary = ParseWebhookPayload(payload);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "WAPIlot webhook payload could not be parsed. PayloadBytes={PayloadBytes}", payload.Length);
            return BadRequest(new
            {
                processed = false,
                provider = "WAPIlot",
                message = "Webhook payload is not valid JSON."
            });
        }

        logger.LogInformation(
            "WAPIlot webhook received. Event={Event} Status={Status} MessageId={MessageId} Phone={Phone} PayloadBytes={PayloadBytes}",
            summary.EventName,
            summary.Status,
            string.IsNullOrWhiteSpace(summary.MessageId) ? "unknown" : summary.MessageId,
            MaskPhone(summary.Phone),
            payload.Length);

        return Ok(new
        {
            processed = true,
            provider = "WAPIlot",
            eventName = summary.EventName,
            status = summary.Status
        });
    }

    private WapilotWebhookSummary ParseWebhookPayload(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        var eventName = FirstNonEmpty(
            ReadString(root, "event"),
            ReadString(root, "type"),
            ReadString(root, "eventType"),
            ReadString(root, "payload", "event")) ?? "unknown";

        var status = FirstNonEmpty(
            ReadString(root, "status"),
            ReadString(root, "messageStatus"),
            ReadString(root, "payload", "status"),
            ReadString(root, "payload", "messageStatus")) ?? "unknown";

        var messageId = FirstNonEmpty(
            ReadString(root, "messageId"),
            ReadString(root, "id"),
            ReadString(root, "payload", "messageId"),
            ReadString(root, "payload", "id"));

        var phone = FirstNonEmpty(
            ReadString(root, "phone"),
            ReadString(root, "from"),
            ReadString(root, "to"),
            ReadString(root, "payload", "phone"),
            ReadString(root, "payload", "from"),
            ReadString(root, "payload", "to"));

        return new WapilotWebhookSummary(eventName, status, messageId, phone);
    }

    private bool IsWebhookSecretValid()
    {
        var expected = settings.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        var provided = FirstNonEmpty(
            Request.Headers[WapilotSecretHeader].ToString(),
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

    private static string? ReadString(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => current.ToString(),
            _ => null
        };
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

    private sealed record WapilotWebhookSummary(
        string EventName,
        string Status,
        string? MessageId,
        string? Phone);
}
