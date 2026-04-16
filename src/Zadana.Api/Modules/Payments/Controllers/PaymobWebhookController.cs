using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Payments.Commands.ProcessPaymobWebhook;

namespace Zadana.Api.Modules.Payments.Controllers;

[Route("api/payments/paymob")]
[Tags("Payments")]
[AllowAnonymous]
public class PaymobWebhookController : ApiControllerBase
{
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
}
