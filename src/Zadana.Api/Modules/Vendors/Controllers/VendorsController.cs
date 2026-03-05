using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.Commands.RegisterVendor;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorProfile;
using Zadana.Application.Modules.Vendors.Queries.GetVendorProfile;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/vendors")]
[Tags("🏪 2. Vendor App API")]
public class VendorsController : ApiControllerBase
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public VendorsController(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    /// <summary>
    /// تسجيل تاجر جديد (يتم مراجعته من SuperAdmin)
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterVendor([FromBody] RegisterVendorCommand command)
    {
        var result = await Sender.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// عرض ملف التاجر الحالي
    /// </summary>
    [HttpGet("profile")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> GetProfile()
    {
        var result = await Sender.Send(new GetVendorProfileQuery());
        return Ok(result);
    }

    /// <summary>
    /// تعديل ملف التاجر
    /// </summary>
    [HttpPut("profile")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateVendorProfileCommand command)
    {
        var result = await Sender.Send(command);
        return Ok(new { Data = result, Message = _localizer["VendorProfileUpdated"].Value });
    }
}
