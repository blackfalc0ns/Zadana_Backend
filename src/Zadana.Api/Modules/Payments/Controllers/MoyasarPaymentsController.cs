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
/// Both routes write the inbound notification through the
/// <c>PaymentProviderEventInbox</c> + <see cref="ProcessPaymentWebhookCommand"/>
/// pipeline so processing is idempotent and auditable.
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

        var secretValid = ValidateWebhookSecret(moyasar, payload);
        if (!secretValid && !environment.IsDevelopment())
        {
            await NotifyIntegrationFailureAsync("Moyasar webhook signature validation failed.", payload, cancellationToken);
        }

        try
        {
            var result = await Sender.Send(
                new ProcessPaymentWebhookCommand(
                    Provider: "Moyasar",
                    Payload: payload,
                    SecretValid: secretValid || environment.IsDevelopment(),
                    Headers: SerializeHeaders()),
                cancellationToken);

            return Ok(new { processed = true, result.PaymentId, result.Status, result.Message });
        }
        catch (Exception ex)
        {
            await NotifyIntegrationFailureAsync($"Moyasar webhook processing failed: {ex.Message}", payload, cancellationToken);
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
            return BadRequest(new { error = "MOYASAR_PAYMENT_ID_REQUIRED" });
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
            return false;
        }

        var providedSignature = Request.Headers[MoyasarSignatureHeader].ToString();

        // Moyasar's webhook config sends a shared "secret_token" inside the JSON body OR as an HMAC header,
        // depending on the dashboard configuration. We accept either. Body-token comparison is constant-time.
        if (!string.IsNullOrWhiteSpace(providedSignature))
        {
            var expected = Convert.ToHexString(
                HMACSHA256.HashData(
                    Encoding.UTF8.GetBytes(moyasar.WebhookSecret),
                    Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected),
                Encoding.ASCII.GetBytes(providedSignature.Trim().ToLowerInvariant()));
        }

        return payload.Contains($"\"secret_token\":\"{moyasar.WebhookSecret}\"", StringComparison.Ordinal);
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
