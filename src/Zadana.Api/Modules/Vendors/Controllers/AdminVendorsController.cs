using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.Commands.ApproveVendor;
using Zadana.Application.Modules.Vendors.Commands.RejectVendor;
using Zadana.Application.Modules.Vendors.Commands.SuspendVendor;
using Zadana.Application.Modules.Vendors.Queries.GetAllVendors;
using Zadana.Application.Modules.Vendors.Queries.GetVendorDetail;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/admin/vendors")]
[Authorize(Policy = "AdminOnly")]
[Tags("👑 4. Admin Dashboard API")]
public class AdminVendorsController : ApiControllerBase
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AdminVendorsController(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    /// <summary>
    /// عرض قائمة التجار مع التصفية والبحث والترقيم
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllVendors(
        [FromQuery] VendorStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await Sender.Send(new GetAllVendorsQuery(status, search, page, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// عرض تفاصيل تاجر معين
    /// </summary>
    [HttpGet("{vendorId:guid}")]
    public async Task<IActionResult> GetVendorDetail(Guid vendorId)
    {
        var result = await Sender.Send(new GetVendorDetailQuery(vendorId));
        return Ok(result);
    }

    /// <summary>
    /// الموافقة على تاجر وتحديد نسبة العمولة
    /// </summary>
    [HttpPost("{vendorId:guid}/approve")]
    public async Task<IActionResult> ApproveVendor(Guid vendorId, [FromBody] ApproveVendorRequest request)
    {
        await Sender.Send(new ApproveVendorCommand(vendorId, request.CommissionRate));
        return Ok(new { Message = _localizer["VendorApprovedSuccessfully"].Value });
    }

    /// <summary>
    /// رفض تاجر مع ذكر السبب
    /// </summary>
    [HttpPost("{vendorId:guid}/reject")]
    public async Task<IActionResult> RejectVendor(Guid vendorId, [FromBody] RejectVendorRequest request)
    {
        await Sender.Send(new RejectVendorCommand(vendorId, request.Reason));
        return Ok(new { Message = _localizer["VendorRejected"].Value });
    }

    /// <summary>
    /// تعليق تاجر نشط
    /// </summary>
    [HttpPost("{vendorId:guid}/suspend")]
    public async Task<IActionResult> SuspendVendor(Guid vendorId, [FromBody] SuspendVendorRequest request)
    {
        await Sender.Send(new SuspendVendorCommand(vendorId, request.Reason));
        return Ok(new { Message = _localizer["VendorSuspended"].Value });
    }
}

public record ApproveVendorRequest(decimal CommissionRate);
public record RejectVendorRequest(string Reason);
public record SuspendVendorRequest(string Reason);
