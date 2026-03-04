using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Vendors.Commands.ApproveVendor;
using Zadana.Application.Modules.Vendors.Commands.RejectVendor;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/admin/vendors")]
[Authorize(Policy = "AdminOnly")]
[Tags("👑 4. Admin Dashboard API")]
public class AdminVendorsController : ApiControllerBase
{
    /// <summary>
    /// الموافقة على تاجر وتحديد نسبة العمولة
    /// </summary>
    [HttpPost("{vendorId:guid}/approve")]
    public async Task<IActionResult> ApproveVendor(Guid vendorId, [FromBody] ApproveVendorRequest request)
    {
        await Sender.Send(new ApproveVendorCommand(vendorId, request.CommissionRate));
        return Ok(new { Message = "تم الموافقة على التاجر بنجاح." });
    }

    /// <summary>
    /// رفض تاجر مع ذكر السبب
    /// </summary>
    [HttpPost("{vendorId:guid}/reject")]
    public async Task<IActionResult> RejectVendor(Guid vendorId, [FromBody] RejectVendorRequest request)
    {
        await Sender.Send(new RejectVendorCommand(vendorId, request.Reason));
        return Ok(new { Message = "تم رفض التاجر." });
    }
}

public record ApproveVendorRequest(decimal CommissionRate);
public record RejectVendorRequest(string Reason);
