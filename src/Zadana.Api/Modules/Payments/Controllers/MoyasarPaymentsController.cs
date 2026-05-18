using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Api.Controllers;
using Zadana.Api.Security;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Payments.Commands.ConfirmCardPayment;
using Zadana.Application.Modules.Payments.Commands.ProcessPaymentWebhook;
using Zadana.Infrastructure.Settings;

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

        var secretValid = ValidateWebhookSecret(moyasar, payload);

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

    private bool ValidateWebhookSecret(MoyasarSettings moyasar, string payload)
    {
        if (string.IsNullOrWhiteSpace(moyasar.WebhookSecret))
        {
            // Development-only soft mode: accept anything so local testing isn't blocked.
            return environment.IsDevelopment();
        }

        var providedSignature = Request.Headers[MoyasarSignatureHeader].ToString();

        // Moyasar's webhook config sends a shared "secret_token" inside the JSON body OR
        // as an HMAC-SHA256 header, depending on the dashboard configuration. We accept
        // either. Both comparisons are constant-time.
        if (!string.IsNullOrWhiteSpace(providedSignature))
        {
            var expected = Convert.ToHexString(
                HMACSHA256.HashData(
                    Encoding.UTF8.GetBytes(moyasar.WebhookSecret),
                    Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

            var provided = providedSignature.Trim().ToLowerInvariant();
            return expected.Length == provided.Length
                && CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected),
                    Encoding.ASCII.GetBytes(provided));
        }

        // Body-token mode. We compare against the configured secret using a
        // constant-time match so attackers can't time their way in.
        var expectedTokenBytes = Encoding.UTF8.GetBytes(moyasar.WebhookSecret);
        var marker = "\"secret_token\":\"";
        var tokenStart = payload.IndexOf(marker, StringComparison.Ordinal);
        if (tokenStart < 0) return false;
        tokenStart += marker.Length;
        var tokenEnd = payload.IndexOf('"', tokenStart);
        if (tokenEnd < 0) return false;
        var providedToken = payload.AsSpan(tokenStart, tokenEnd - tokenStart);
        var providedTokenBytes = Encoding.UTF8.GetBytes(providedToken.ToString());
        return providedTokenBytes.Length == expectedTokenBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedTokenBytes, expectedTokenBytes);
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
