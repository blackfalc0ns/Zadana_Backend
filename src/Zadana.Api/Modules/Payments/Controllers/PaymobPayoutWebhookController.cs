using Microsoft.AspNetCore.Mvc;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Payments.Interfaces;

namespace Zadana.Api.Modules.Payments.Controllers;

[ApiController]
[Route("api/payments/paymob/payout-webhook")]
public sealed class PaymobPayoutWebhookController(
    IPaymobPayoutGateway payoutGateway,
    PaymobPayoutOrchestrator payoutOrchestrator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawPayload = await reader.ReadToEndAsync(cancellationToken);
        var notification = payoutGateway.ParsePayoutWebhook(rawPayload);
        await payoutOrchestrator.ApplyProviderCallbackAsync(notification, cancellationToken);
        return Ok(new { processed = true });
    }
}
