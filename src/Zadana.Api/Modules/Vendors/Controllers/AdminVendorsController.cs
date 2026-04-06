using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Vendors.Requests;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.Commands.AdminResetVendorPassword;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorLegalBanking;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorOwner;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorStore;
using Zadana.Application.Modules.Vendors.Commands.ApproveVendor;
using Zadana.Application.Modules.Vendors.Commands.ArchiveVendor;
using Zadana.Application.Modules.Vendors.Commands.LockVendorLogin;
using Zadana.Application.Modules.Vendors.Commands.RejectVendor;
using Zadana.Application.Modules.Vendors.Commands.SuspendVendor;
using Zadana.Application.Modules.Vendors.Commands.UnlockVendorLogin;
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

    [HttpPost("{vendorId:guid}/lock-login")]
    public async Task<IActionResult> LockLogin(Guid vendorId, [FromBody] LockVendorLoginRequest request)
    {
        await Sender.Send(new LockVendorLoginCommand(vendorId, request.Reason));
        return Ok(new { Message = "Vendor login locked successfully." });
    }

    [HttpPost("{vendorId:guid}/unlock-login")]
    public async Task<IActionResult> UnlockLogin(Guid vendorId)
    {
        await Sender.Send(new UnlockVendorLoginCommand(vendorId));
        return Ok(new { Message = "Vendor login unlocked successfully." });
    }

    [HttpPost("{vendorId:guid}/archive")]
    public async Task<IActionResult> ArchiveVendor(Guid vendorId, [FromBody] ArchiveVendorRequest request)
    {
        await Sender.Send(new ArchiveVendorCommand(vendorId, request.Reason));
        return Ok(new { Message = "Vendor archived successfully." });
    }

    [HttpPost("{vendorId:guid}/reset-password")]
    public async Task<IActionResult> ResetVendorPassword(Guid vendorId, [FromBody] AdminResetVendorPasswordRequest request)
    {
        await Sender.Send(new AdminResetVendorPasswordCommand(vendorId, request.NewPassword));
        return Ok(new { Message = "Vendor password reset successfully." });
    }

    [HttpPut("{vendorId:guid}/store")]
    public async Task<IActionResult> UpdateStore(Guid vendorId, [FromBody] AdminUpdateVendorStoreRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorStoreCommand(
            vendorId,
            request.BusinessNameAr,
            request.BusinessNameEn,
            request.BusinessType,
            request.ContactEmail,
            request.ContactPhone,
            request.DescriptionAr,
            request.DescriptionEn,
            request.LogoUrl,
            request.CommercialRegisterDocumentUrl));

        return Ok(result);
    }

    [HttpPut("{vendorId:guid}/owner")]
    public async Task<IActionResult> UpdateOwner(Guid vendorId, [FromBody] AdminUpdateVendorOwnerRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorOwnerCommand(
            vendorId,
            request.OwnerName,
            request.OwnerEmail,
            request.OwnerPhone,
            request.IdNumber,
            request.Nationality));

        return Ok(result);
    }

    [HttpPut("{vendorId:guid}/legal-banking")]
    public async Task<IActionResult> UpdateLegalBanking(Guid vendorId, [FromBody] AdminUpdateVendorLegalBankingRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorLegalBankingCommand(
            vendorId,
            request.CommercialRegistrationNumber,
            request.CommercialRegistrationExpiryDate,
            request.TaxId,
            request.LicenseNumber,
            request.BankName,
            request.AccountHolderName,
            request.Iban,
            request.SwiftCode,
            request.PayoutCycle,
            request.CommercialRegisterDocumentUrl));

        return Ok(result);
    }
}
