using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Vendors.Commands.RegisterVendor;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/vendors")]
[Tags("🏪 Vendor App")]
public class VendorsController : ApiControllerBase
{
    /// <summary>
    /// تسجيل تاجر جديد (يتم مراجعته من SuperAdmin)
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterVendor([FromBody] RegisterVendorCommand command)
    {
        var result = await Sender.Send(command);
        return Ok(result);
    }
}
