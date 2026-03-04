using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Delivery.Commands.RegisterDriver;

namespace Zadana.Api.Modules.Delivery.Controllers;

[Route("api/drivers")]
[Tags("🛵 3. Driver App API")]
public class DriversController : ApiControllerBase
{
    /// <summary>
    /// تسجيل مندوب توصيل جديد (جاري مراجعة بياناتك)
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterDriver([FromBody] RegisterDriverCommand command)
    {
        var result = await Sender.Send(command);
        return Ok(result);
    }
}
