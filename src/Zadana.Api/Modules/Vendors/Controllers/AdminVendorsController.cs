using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Vendors.Requests;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Queries.GetVendorProducts;
using Zadana.Application.Modules.Orders.Queries.GetVendorOrders;
using Zadana.Application.Modules.Vendors.Commands.AdminResetVendorPassword;
using Zadana.Application.Modules.Vendors.Commands.AddVendorReviewNote;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorLegalBanking;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorContact;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorHours;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorNotificationSettings;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorOwner;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorOperationsSettings;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorStore;
using Zadana.Application.Modules.Vendors.Commands.ApproveVendor;
using Zadana.Application.Modules.Vendors.Commands.ArchiveVendor;
using Zadana.Application.Modules.Vendors.Commands.LockVendorLogin;
using Zadana.Application.Modules.Vendors.Commands.RejectVendor;
using Zadana.Application.Modules.Vendors.Commands.ReactivateVendor;
using Zadana.Application.Modules.Vendors.Commands.RequestVendorDocuments;
using Zadana.Application.Modules.Vendors.Commands.StartVendorReview;
using Zadana.Application.Modules.Vendors.Commands.SuspendVendor;
using Zadana.Application.Modules.Vendors.Commands.UnlockVendorLogin;
using Zadana.Application.Modules.Vendors.Queries.GetAllVendors;
using Zadana.Application.Modules.Vendors.Queries.GetVendorDetail;
using Zadana.Application.Modules.Wallets.Commands.CreateSettlement;
using Zadana.Application.Modules.Wallets.Commands.EscalateVendorPayout;
using Zadana.Application.Modules.Wallets.Commands.RetryVendorPayout;
using Zadana.Application.Modules.Wallets.Commands.SuspendVendorPayout;
using Zadana.Application.Modules.Wallets.Queries.GetVendorPayouts;
using Zadana.Application.Modules.Wallets.Queries.GetVendorSettlements;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/admin/vendors")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin Dashboard API")]
public class AdminVendorsController : ApiControllerBase
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AdminVendorsController(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    /// <summary>
    /// Ø¹Ø±Ø¶ Ù‚Ø§Ø¦Ù…Ø© Ø§Ù„ØªØ¬Ø§Ø± Ù…Ø¹ Ø§Ù„ØªØµÙÙŠØ© ÙˆØ§Ù„Ø¨Ø­Ø« ÙˆØ§Ù„ØªØ±Ù‚ÙŠÙ…
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
    /// Ø¹Ø±Ø¶ ØªÙØ§ØµÙŠÙ„ ØªØ§Ø¬Ø± Ù…Ø¹ÙŠÙ†
    /// </summary>
    [HttpGet("{vendorId:guid}")]
    public async Task<IActionResult> GetVendorDetail(Guid vendorId)
    {
        var result = await Sender.Send(new GetVendorDetailQuery(vendorId));
        return Ok(result);
    }

    [HttpGet("{vendorId:guid}/orders")]
    public async Task<IActionResult> GetVendorOrders(
        Guid vendorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await Sender.Send(new GetVendorOrdersQuery(vendorId, page, pageSize));
        return Ok(result);
    }

    [HttpGet("{vendorId:guid}/products")]
    public async Task<IActionResult> GetVendorProducts(
        Guid vendorId,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? branchId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await Sender.Send(new GetVendorProductsQuery(vendorId, categoryId, branchId, page, pageSize));
        return Ok(result);
    }

    [HttpGet("{vendorId:guid}/settlements")]
    public async Task<IActionResult> GetVendorSettlements(
        Guid vendorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Sender.Send(new GetVendorSettlementsQuery(vendorId, page, pageSize));
        return Ok(result);
    }

    [HttpGet("{vendorId:guid}/payouts")]
    public async Task<IActionResult> GetVendorPayouts(
        Guid vendorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Sender.Send(new GetVendorPayoutsQuery(vendorId, page, pageSize));
        return Ok(result);
    }

    [HttpPost("{vendorId:guid}/settlements")]
    public async Task<IActionResult> CreateVendorSettlement(Guid vendorId, [FromBody] AdminCreateVendorSettlementRequest request)
    {
        var settlementId = await Sender.Send(new CreateSettlementCommand(
            vendorId,
            null,
            request.GrossAmount,
            request.CommissionAmount,
            request.NetAmount));

        return Ok(new { SettlementId = settlementId });
    }

    [HttpPost("{vendorId:guid}/start-review")]
    public async Task<IActionResult> StartVendorReview(Guid vendorId)
    {
        var result = await Sender.Send(new StartVendorReviewCommand(vendorId));
        return Ok(result);
    }

    [HttpPost("{vendorId:guid}/request-documents")]
    public async Task<IActionResult> RequestVendorDocuments(Guid vendorId, [FromBody] AdminRequestVendorDocumentsRequest request)
    {
        var result = await Sender.Send(new RequestVendorDocumentsCommand(vendorId, request.Note));
        return Ok(result);
    }

    [HttpPost("{vendorId:guid}/review-notes")]
    public async Task<IActionResult> AddVendorReviewNote(Guid vendorId, [FromBody] AdminAddVendorReviewNoteRequest request)
    {
        var result = await Sender.Send(new AddVendorReviewNoteCommand(
            vendorId,
            request.Message,
            request.AuthorName,
            request.RoleLabel));

        return Ok(result);
    }

    [HttpPost("{vendorId:guid}/payouts/{payoutId:guid}/retry")]
    public async Task<IActionResult> RetryVendorPayout(Guid vendorId, Guid payoutId)
    {
        await Sender.Send(new RetryVendorPayoutCommand(vendorId, payoutId));
        return Ok(new { Message = "Vendor payout moved back to processing." });
    }

    [HttpPost("{vendorId:guid}/payouts/{payoutId:guid}/suspend")]
    public async Task<IActionResult> SuspendVendorPayout(Guid vendorId, Guid payoutId)
    {
        await Sender.Send(new SuspendVendorPayoutCommand(vendorId, payoutId));
        return Ok(new { Message = "Vendor payout suspended successfully." });
    }

    [HttpPost("{vendorId:guid}/payouts/{payoutId:guid}/escalate")]
    public async Task<IActionResult> EscalateVendorPayout(Guid vendorId, Guid payoutId)
    {
        await Sender.Send(new EscalateVendorPayoutCommand(vendorId, payoutId));
        return Ok(new { Message = "Vendor payout escalated successfully." });
    }

    /// <summary>
    /// Ø§Ù„Ù…ÙˆØ§ÙÙ‚Ø© Ø¹Ù„Ù‰ ØªØ§Ø¬Ø± ÙˆØªØ­Ø¯ÙŠØ¯ Ù†Ø³Ø¨Ø© Ø§Ù„Ø¹Ù…ÙˆÙ„Ø©
    /// </summary>
    [HttpPost("{vendorId:guid}/approve")]
    public async Task<IActionResult> ApproveVendor(Guid vendorId, [FromBody] ApproveVendorRequest request)
    {
        await Sender.Send(new ApproveVendorCommand(vendorId, request.CommissionRate));
        return Ok(new { Message = _localizer["VendorApprovedSuccessfully"].Value });
    }

    /// <summary>
    /// Ø±ÙØ¶ ØªØ§Ø¬Ø± Ù…Ø¹ Ø°ÙƒØ± Ø§Ù„Ø³Ø¨Ø¨
    /// </summary>
    [HttpPost("{vendorId:guid}/reject")]
    public async Task<IActionResult> RejectVendor(Guid vendorId, [FromBody] RejectVendorRequest request)
    {
        await Sender.Send(new RejectVendorCommand(vendorId, request.Reason));
        return Ok(new { Message = _localizer["VendorRejected"].Value });
    }

    /// <summary>
    /// ØªØ¹Ù„ÙŠÙ‚ ØªØ§Ø¬Ø± Ù†Ø´Ø·
    /// </summary>
    [HttpPost("{vendorId:guid}/suspend")]
    public async Task<IActionResult> SuspendVendor(Guid vendorId, [FromBody] SuspendVendorRequest request)
    {
        await Sender.Send(new SuspendVendorCommand(vendorId, request.Reason));
        return Ok(new { Message = _localizer["VendorSuspended"].Value });
    }

    [HttpPost("{vendorId:guid}/reactivate")]
    public async Task<IActionResult> ReactivateVendor(Guid vendorId)
    {
        await Sender.Send(new ReactivateVendorCommand(vendorId));
        return Ok(new { Message = "Vendor reactivated successfully." });
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
            request.CommercialRegisterDocumentUrl,
            request.Region,
            request.City,
            request.NationalAddress,
            request.CommercialRegistrationNumber));

        return Ok(result);
    }

    [HttpPut("{vendorId:guid}/contact")]
    public async Task<IActionResult> UpdateContact(Guid vendorId, [FromBody] AdminUpdateVendorContactRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorContactCommand(
            vendorId,
            request.Region,
            request.City,
            request.NationalAddress));

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

    [HttpPut("{vendorId:guid}/hours")]
    public async Task<IActionResult> UpdateHours(Guid vendorId, [FromBody] AdminUpdateVendorHoursRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorHoursCommand(
            vendorId,
            request.Hours.Select(item => new AdminUpdateVendorHoursItem(
                item.DayOfWeek,
                item.OpenTime,
                item.CloseTime,
                item.IsOpen)).ToList()));

        return Ok(result);
    }

    [HttpPut("{vendorId:guid}/operations-settings")]
    public async Task<IActionResult> UpdateOperationsSettings(Guid vendorId, [FromBody] AdminUpdateVendorOperationsSettingsRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorOperationsSettingsCommand(
            vendorId,
            request.AcceptOrders,
            request.MinimumOrderAmount,
            request.PreparationTimeMinutes));

        return Ok(result);
    }

    [HttpPut("{vendorId:guid}/notification-settings")]
    public async Task<IActionResult> UpdateNotificationSettings(Guid vendorId, [FromBody] AdminUpdateVendorNotificationSettingsRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorNotificationSettingsCommand(
            vendorId,
            request.EmailNotificationsEnabled,
            request.SmsNotificationsEnabled,
            request.NewOrdersNotificationsEnabled));

        return Ok(result);
    }
}

