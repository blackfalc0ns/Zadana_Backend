using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Payments.Interfaces;

namespace Zadana.Api.Modules.Payments.Controllers;

[ApiController]
[Route("api/payments/paymob/payout-webhook")]
public sealed class PaymobPayoutWebhookController(
    IPaymobPayoutGateway payoutGateway,
    PaymobPayoutOrchestrator payoutOrchestrator,
    IAdminAlertService adminAlertService,
    ILogger<PaymobPayoutWebhookController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawPayload = await reader.ReadToEndAsync(cancellationToken);

        try
        {
            var notification = payoutGateway.ParsePayoutWebhook(rawPayload);
            await payoutOrchestrator.ApplyProviderCallbackAsync(notification, cancellationToken);
            return Ok(new { processed = true });
        }
        catch (Exception ex)
        {
            await NotifyIntegrationFailureAsync(ex, rawPayload, cancellationToken);
            throw;
        }
    }

    private async Task NotifyIntegrationFailureAsync(
        Exception exception,
        string rawPayload,
        CancellationToken cancellationToken)
    {
        try
        {
            await adminAlertService.SendAsync(
                new AdminAlertRequest(
                    AdminAlertTypes.SystemIntegrationFailure,
                    AdminAlertCategories.System,
                    AdminAlertPriorities.Critical,
                    "فشل Paymob Payout Webhook",
                    "Paymob payout webhook failure",
                    "حدث خطأ أثناء معالجة webhook تحويلات Paymob.",
                    "Paymob payout webhook processing failed.",
                    null,
                    "/finances/settlements",
                    new
                    {
                        integration = "PaymobPayouts",
                        exceptionType = exception.GetType().Name,
                        message = exception.Message,
                        payloadLength = rawPayload.Length
                    }),
                cancellationToken);
        }
        catch (Exception alertException)
        {
            logger.LogError(
                alertException,
                "Failed to dispatch Paymob payout webhook integration failure admin alert.");
        }
    }
}
