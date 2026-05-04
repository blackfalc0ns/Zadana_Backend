using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zadana.Api.Controllers;
using Zadana.Api.Security;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Payments.Commands.ConfirmPaymobPayment;
using Zadana.Application.Modules.Payments.Commands.ProcessPaymobWebhook;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Payments.Controllers;

[Route("api/payments/paymob")]
[Tags("Payments")]
public class PaymobWebhookController(
    IPaymobGateway paymobGateway,
    IWebHostEnvironment environment) : ApiControllerBase
{
    private const string DeviceIdHeader = "X-Device-Id";

    [AllowAnonymous]
    [HttpPost("webhook")]
    [EnableRateLimiting(RateLimitPolicyNames.PaymentCallbacks)]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken = default)
    {
        if (!paymobGateway.IsWebhookTrusted() && !environment.IsDevelopment())
        {
            throw new BusinessRuleException("PAYMOB_WEBHOOK_SECURITY_NOT_CONFIGURED", "Paymob webhook HMAC validation must be configured outside development.");
        }

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var result = await Sender.Send(new ProcessPaymobWebhookCommand(payload), cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("return")]
    [EnableRateLimiting(RateLimitPolicyNames.PaymentCallbacks)]
    public async Task<ActionResult<ConfirmPaymobPaymentResponse>> ConfirmPaymentReturn(
        [FromQuery] PaymobReturnRequest? request,
        CancellationToken cancellationToken = default)
    {
        var paymentId = Guid.TryParse(request?.MerchantOrderId, out var parsedPaymentId)
            ? parsedPaymentId
            : request?.PaymentId;

        var result = await Sender.Send(
            new ConfirmPaymobPaymentCommand(
                paymentId,
                null,
                request?.ProviderReference,
                request?.ProviderTransactionId,
                null,
                true,
                ResolveDeviceIdHeader()),
            cancellationToken);

        return Ok(new ConfirmPaymobPaymentResponse(
            result.MessageAr,
            result.MessageEn,
            result.PaymentId,
            result.PaymentStatus,
            result.UserId,
            result.OrderId,
            result.OrderStatus,
            result.AlreadyConfirmed));
    }

    private string? ResolveDeviceIdHeader()
    {
        var deviceId = Request.Headers[DeviceIdHeader].ToString();
        return string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
    }

}

public record ConfirmPaymobPaymentResponse(
    string MessageAr,
    string MessageEn,
    Guid PaymentId,
    string PaymentStatus,
    Guid UserId,
    Guid OrderId,
    string OrderStatus,
    bool AlreadyConfirmed);

public record PaymobReturnRequest(
    [property: FromQuery(Name = "paymentId")] Guid? PaymentId,
    [property: FromQuery(Name = "merchant_order_id")] string? MerchantOrderId,
    [property: FromQuery(Name = "order")] string? ProviderReference,
    [property: FromQuery(Name = "id")] string? ProviderTransactionId,
    [property: FromQuery(Name = "success")] bool? IsSuccess,
    [property: FromQuery(Name = "pending")] bool? IsPending);

    
