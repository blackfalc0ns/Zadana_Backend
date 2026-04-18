using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Payments.Commands.ConfirmPaymobPayment;
using Zadana.Application.Modules.Payments.Commands.ProcessPaymobWebhook;

namespace Zadana.Api.Modules.Payments.Controllers;

[Route("api/payments/paymob")]
[Tags("Payments")]
public class PaymobWebhookController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUserService;

    public PaymobWebhookController(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

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

    [Authorize(Policy = "CustomerOnly")]
    [HttpPost("confirm")]
    public async Task<ActionResult<ConfirmPaymobPaymentResponse>> ConfirmPayment(
        [FromBody] ConfirmPaymobPaymentRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest();
        }

        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new ConfirmPaymobPaymentCommand(
                request.PaymentId,
                request.Payload,
                request.ProviderReference,
                request.ProviderTransactionId,
                request.IsSuccess,
                request.IsPending,
                userId),
            cancellationToken);

        return Ok(new ConfirmPaymobPaymentResponse(
            result.Message,
            result.PaymentId,
            result.PaymentStatus,
            result.OrderId,
            result.OrderStatus,
            result.AlreadyConfirmed));
    }
}

public record ConfirmPaymobPaymentRequest(
    Guid? PaymentId,
    string? Payload,
    string? ProviderReference,
    string? ProviderTransactionId,
    bool? IsSuccess,
    bool? IsPending);

public record ConfirmPaymobPaymentResponse(
    string Message,
    Guid PaymentId,
    string PaymentStatus,
    Guid OrderId,
    string OrderStatus,
    bool AlreadyConfirmed);
