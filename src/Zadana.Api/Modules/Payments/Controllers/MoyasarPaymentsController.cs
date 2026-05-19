using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Api.Controllers;
using Zadana.Api.Security;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Payments.Commands.ConfirmCardPayment;
using Zadana.Application.Modules.Payments.Commands.ProcessPaymentWebhook;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Finance;

namespace Zadana.Api.Modules.Payments.Controllers;

/// <summary>
/// Endpoints exposed to Moyasar for callback (return URL) and webhook delivery.
/// Webhooks are written to <c>PaymentProviderEventInbox</c> for idempotency
/// and audit; processing is delegated to <see cref="ProcessPaymentWebhookCommand"/>.
/// </summary>
[Route("api/payments/moyasar")]
[Tags("Payments")]
public class MoyasarPaymentsController(
    IOptions<MoyasarSettings> settings,
    IWebHostEnvironment environment,
    IAdminAlertService adminAlertService,
    PayoutOrchestrator payoutOrchestrator,
    ILogger<MoyasarPaymentsController> logger) : ApiControllerBase
{
    private const string DeviceIdHeader = "X-Device-Id";
    private const string MoyasarSignatureHeader = "X-Moyasar-Signature";

    [AllowAnonymous]
    [HttpPost("webhook")]
    [EnableRateLimiting(RateLimitPolicyNames.PaymentCallbacks)]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken = default)
    {
        var moyasar = settings.Value;

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        // Config guard: in non-development, a missing WebhookSecret is a deployment
        // problem (not the caller's fault). Tell ops, return 503 so Moyasar retries
        // once the config is fixed.
        if (string.IsNullOrWhiteSpace(moyasar.WebhookSecret) && !environment.IsDevelopment())
        {
            await NotifyIntegrationFailureAsync(
                "Moyasar:WebhookSecret is not configured.",
                payload,
                cancellationToken);

            return Problem(
                title: "Moyasar webhook is not configured",
                detail: "Moyasar webhook secret is not configured on the server. Contact administrator.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://moyasar.runasp.net/errors/PAYMENT_WEBHOOK_NOT_CONFIGURED",
                instance: "/api/payments/moyasar/webhook");
        }

        var secretValid = MoyasarWebhookSecretValidator.Validate(
            moyasar.WebhookSecret,
            payload,
            Request.Headers[MoyasarSignatureHeader].ToString(),
            environment.IsDevelopment());

        // Untrusted source: tell Moyasar we will not process; do not retry forever.
        // 401 is the "stop, your credentials are wrong" signal. We still record the
        // attempt in the inbox for audit so ops can spot stuffing attempts.
        if (!secretValid)
        {
            await Sender.Send(
                new ProcessPaymentWebhookCommand(
                    Provider: "Moyasar",
                    Payload: payload,
                    SecretValid: false,
                    Headers: SerializeHeaders()),
                cancellationToken);

            await NotifyIntegrationFailureAsync(
                "Moyasar webhook signature validation failed.",
                payload,
                cancellationToken);

            return Problem(
                title: "Webhook signature invalid",
                detail: "Webhook signature could not be validated.",
                statusCode: StatusCodes.Status401Unauthorized,
                type: "https://moyasar.runasp.net/errors/PAYMENT_WEBHOOK_INVALID_SIGNATURE",
                instance: "/api/payments/moyasar/webhook");
        }

        try
        {
            var result = await Sender.Send(
                new ProcessPaymentWebhookCommand(
                    Provider: "Moyasar",
                    Payload: payload,
                    SecretValid: true,
                    Headers: SerializeHeaders()),
                cancellationToken);

            return Ok(new
            {
                processed = true,
                paymentId = result.PaymentId,
                status = result.Status,
                message = result.Message,
            });
        }
        catch (Exception ex)
        {
            await NotifyIntegrationFailureAsync(
                $"Moyasar webhook processing failed: {ex.Message}",
                payload,
                cancellationToken);

            throw;
        }
    }

    [AllowAnonymous]
    [HttpPost("payouts/webhook")]
    [EnableRateLimiting(RateLimitPolicyNames.PaymentCallbacks)]
    public async Task<IActionResult> ReceivePayoutWebhook(CancellationToken cancellationToken = default)
    {
        var moyasar = settings.Value;

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(moyasar.WebhookSecret) && !environment.IsDevelopment())
        {
            await NotifyIntegrationFailureAsync(
                "Moyasar:WebhookSecret is not configured for payout webhook.",
                payload,
                cancellationToken);

            return Problem(
                title: "Moyasar payout webhook is not configured",
                detail: "Moyasar webhook secret is not configured on the server. Contact administrator.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                type: "https://moyasar.runasp.net/errors/PAYOUT_WEBHOOK_NOT_CONFIGURED",
                instance: "/api/payments/moyasar/payouts/webhook");
        }

        var secretValid = MoyasarWebhookSecretValidator.Validate(
            moyasar.WebhookSecret,
            payload,
            Request.Headers[MoyasarSignatureHeader].ToString(),
            environment.IsDevelopment());

        if (!secretValid)
        {
            await NotifyIntegrationFailureAsync(
                "Moyasar payout webhook signature validation failed.",
                payload,
                cancellationToken);

            return Problem(
                title: "Webhook signature invalid",
                detail: "Webhook signature could not be validated.",
                statusCode: StatusCodes.Status401Unauthorized,
                type: "https://moyasar.runasp.net/errors/PAYOUT_WEBHOOK_INVALID_SIGNATURE",
                instance: "/api/payments/moyasar/payouts/webhook");
        }

        if (!TryParsePayoutWebhook(payload, out var details, out var parseError))
        {
            await NotifyIntegrationFailureAsync(
                $"Moyasar payout webhook payload could not be parsed: {parseError}",
                payload,
                cancellationToken);

            return BadRequest(new
            {
                processed = false,
                message = parseError
            });
        }

        var payout = await payoutOrchestrator.ApplyProviderStatusAsync(details, cancellationToken);

        return Ok(new
        {
            processed = payout is not null,
            payoutId = payout?.Id,
            status = payout?.Status.ToString() ?? details.ProviderStatus,
            providerTransferId = details.ProviderTransferId,
            providerSequenceNumber = details.ProviderSequenceNumber
        });
    }

    [AllowAnonymous]
    [HttpGet("verify")]
    [EnableRateLimiting(RateLimitPolicyNames.PaymentCallbacks)]
    public async Task<ActionResult<ConfirmCardPaymentResponse>> Verify(
        [FromQuery] MoyasarReturnRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Id))
        {
            return Problem(
                title: "Moyasar payment id is required",
                detail: "The Moyasar 'id' query parameter is missing. Make sure the Moyasar form was configured with a callback that propagates the payment id.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://moyasar.runasp.net/errors/MOYASAR_PAYMENT_ID_REQUIRED",
                instance: "/api/payments/moyasar/verify");
        }

        var result = await Sender.Send(
            new ConfirmCardPaymentCommand(
                PaymentId: null,
                ProviderPaymentId: request.Id,
                ProviderName: "Moyasar",
                CustomerDeviceId: ResolveDeviceIdHeader()),
            cancellationToken);

        return Ok(new ConfirmCardPaymentResponse(
            result.Message,
            result.PaymentId,
            result.PaymentStatus,
            result.UserId,
            result.OrderId,
            result.OrderStatus,
            result.AlreadyConfirmed));
    }

    private string? SerializeHeaders()
    {
        try
        {
            var dict = Request.Headers
                .Where(h => !string.Equals(h.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(h => h.Key, h => string.Join(",", h.Value.ToArray()));
            return System.Text.Json.JsonSerializer.Serialize(dict);
        }
        catch
        {
            return null;
        }
    }

    private string? ResolveDeviceIdHeader()
    {
        var deviceId = Request.Headers[DeviceIdHeader].ToString();
        return string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
    }

    private async Task NotifyIntegrationFailureAsync(string reason, string? payload, CancellationToken cancellationToken)
    {
        try
        {
            await adminAlertService.SendAsync(
                new AdminAlertRequest(
                    AdminAlertTypes.SystemIntegrationFailure,
                    AdminAlertCategories.System,
                    AdminAlertPriorities.Critical,
                    "فشل تكامل Moyasar",
                    "Moyasar integration failure",
                    "حدث خطأ أثناء معالجة webhook الدفع من Moyasar.",
                    "Moyasar payment webhook processing failed.",
                    null,
                    "/finances",
                    new
                    {
                        integration = "MoyasarPayments",
                        reason,
                        payloadLength = payload?.Length ?? 0,
                    }),
                cancellationToken);
        }
        catch (Exception alertException)
        {
            logger.LogError(alertException, "Failed to dispatch Moyasar integration failure admin alert.");
        }
    }

    private static bool TryParsePayoutWebhook(
        string payload,
        out PayoutGatewayDetails details,
        out string? error)
    {
        details = null!;
        error = null;

        if (string.IsNullOrWhiteSpace(payload))
        {
            error = "Payload is empty.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var providerTransferId =
                ReadJsonString(root, "id") ??
                ReadJsonString(root, "data", "id") ??
                ReadJsonString(root, "payout", "id");
            var providerSequenceNumber =
                ReadJsonString(root, "sequence_number") ??
                ReadJsonString(root, "data", "sequence_number") ??
                ReadJsonString(root, "payout", "sequence_number");

            if (string.IsNullOrWhiteSpace(providerTransferId) &&
                string.IsNullOrWhiteSpace(providerSequenceNumber))
            {
                error = "Provider payout id or sequence_number is required.";
                return false;
            }

            var status =
                ReadJsonString(root, "status") ??
                ReadJsonString(root, "data", "status") ??
                ReadJsonString(root, "payout", "status") ??
                "unknown";
            var amountMinor =
                ReadJsonInt64(root, "amount") ??
                ReadJsonInt64(root, "data", "amount") ??
                ReadJsonInt64(root, "payout", "amount") ??
                0L;
            var currency =
                ReadJsonString(root, "currency") ??
                ReadJsonString(root, "data", "currency") ??
                ReadJsonString(root, "payout", "currency") ??
                CurrencyPolicy.OfficialCurrency;
            var failureMessage =
                ReadJsonString(root, "failure_reason") ??
                ReadJsonString(root, "data", "failure_reason") ??
                ReadJsonString(root, "payout", "failure_reason") ??
                ReadJsonString(root, "message") ??
                ReadJsonString(root, "data", "message");

            details = new PayoutGatewayDetails(
                "Moyasar",
                providerTransferId ?? string.Empty,
                status,
                CurrencyPolicy.FromMinorUnits(amountMinor, currency),
                currency.ToUpperInvariant(),
                CompletedAtUtc: null,
                FailureMessage: string.IsNullOrWhiteSpace(failureMessage) ? null : failureMessage,
                RawResponse: payload,
                ProviderSequenceNumber: providerSequenceNumber);
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string? ReadJsonString(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => current.GetRawText()
        };
    }

    private static long? ReadJsonInt64(JsonElement root, params string[] path)
    {
        var raw = ReadJsonString(root, path);
        return long.TryParse(raw, out var value) ? value : null;
    }
}

public record MoyasarReturnRequest(
    [property: FromQuery(Name = "id")] string? Id,
    [property: FromQuery(Name = "status")] string? Status,
    [property: FromQuery(Name = "message")] string? Message);

public record ConfirmCardPaymentResponse(
    string Message,
    Guid PaymentId,
    string PaymentStatus,
    Guid UserId,
    Guid OrderId,
    string OrderStatus,
    bool AlreadyConfirmed);

public static class MoyasarWebhookSecretValidator
{
    public static bool Validate(
        string configuredSecret,
        string payload,
        string? providedSignature,
        bool allowMissingSecret)
    {
        if (string.IsNullOrWhiteSpace(configuredSecret))
        {
            return allowMissingSecret;
        }

        if (!string.IsNullOrWhiteSpace(providedSignature)
            && ValidateSignature(configuredSecret, payload, providedSignature))
        {
            return true;
        }

        return TryReadBodySecretToken(payload, out var providedToken)
            && FixedTimeEquals(configuredSecret, providedToken);
    }

    private static bool ValidateSignature(string configuredSecret, string payload, string providedSignature)
    {
        var expected = Convert.ToHexString(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(configuredSecret),
                Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        var provided = providedSignature.Trim().ToLowerInvariant();
        const string sha256Prefix = "sha256=";
        if (provided.StartsWith(sha256Prefix, StringComparison.OrdinalIgnoreCase))
        {
            provided = provided[sha256Prefix.Length..];
        }

        return FixedTimeEquals(expected, provided);
    }

    private static bool TryReadBodySecretToken(string payload, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "secret_token", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    token = property.Value.GetString() ?? string.Empty;
                    return !string.IsNullOrWhiteSpace(token);
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool FixedTimeEquals(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);

        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
