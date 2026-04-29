using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Payments.Commands.ConfirmPaymobPayment;
using Zadana.Application.Modules.Payments.Commands.ProcessPaymobWebhook;

namespace Zadana.Api.Modules.Payments.Controllers;

[Route("api/payments/paymob")]
[Tags("Payments")]
public class PaymobWebhookController : ApiControllerBase
{
    private const string DeviceIdHeader = "X-Device-Id";

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken = default)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var result = await Sender.Send(new ProcessPaymobWebhookCommand(payload), cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("return")]
    public async Task<ActionResult<ConfirmPaymobPaymentResponse>> ConfirmPaymentReturn(
        [FromQuery] PaymobReturnRequest? request,
        CancellationToken cancellationToken = default)
    {
        var paymentId = Guid.TryParse(request?.MerchantOrderId, out var parsedPaymentId)
            ? parsedPaymentId
            : request?.PaymentId;
        var inferredSuccess = InferReturnSuccess(request, paymentId);

        var result = await Sender.Send(
            new ConfirmPaymobPaymentCommand(
                paymentId,
                null,
                request?.ProviderReference,
                request?.ProviderTransactionId,
                inferredSuccess,
                request?.IsPending,
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

    private static bool? InferReturnSuccess(PaymobReturnRequest? request, Guid? paymentId)
    {
        if (request?.IsSuccess.HasValue == true)
        {
            return request.IsSuccess.Value;
        }

        if (request?.IsPending == true)
        {
            return false;
        }

        var hasPaymobIdentifiers =
            paymentId.HasValue ||
            !string.IsNullOrWhiteSpace(request?.MerchantOrderId) ||
            !string.IsNullOrWhiteSpace(request?.ProviderReference) ||
            !string.IsNullOrWhiteSpace(request?.ProviderTransactionId);

        return hasPaymobIdentifiers ? true : null;
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

    
