using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Api.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Vendors.Controllers;

/// <summary>
/// Admin endpoints for managing vendor bank accounts (verify, reject, set primary)
/// </summary>
[Route("api/admin/vendors/{vendorId:guid}/bank-accounts")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin Dashboard API")]
public class AdminVendorBankAccountsController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public AdminVendorBankAccountsController(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
    }

    [HttpGet]
    public async Task<ActionResult> GetBankAccounts(Guid vendorId, CancellationToken ct)
    {
        await RequireVendorAsync(vendorId, ct);

        var accounts = await _context.VendorBankAccounts
            .AsNoTracking()
            .Where(a => a.VendorId == vendorId)
            .OrderByDescending(a => a.IsPrimary)
            .ThenByDescending(a => a.CreatedAtUtc)
            .Select(a => new
            {
                a.Id,
                a.BankName,
                a.AccountHolderName,
                a.IBAN,
                a.SwiftCode,
                a.IsPrimary,
                Status = a.Status.ToString(),
                a.RejectionReason,
                a.VerifiedAtUtc,
                a.VerifiedBy,
                a.CreatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(accounts);
    }

    [HttpPost("{accountId:guid}/verify")]
    public async Task<ActionResult> VerifyBankAccount(Guid vendorId, Guid accountId, CancellationToken ct)
    {
        await RequireVendorAsync(vendorId, ct);
        var account = await RequireAccountAsync(vendorId, accountId, ct);
        var adminId = _currentUserService.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        account.Verify(adminId);
        await _context.SaveChangesAsync(ct);

        // Notify vendor
        var vendorUserId = await _context.Vendors
            .AsNoTracking()
            .Where(v => v.Id == vendorId)
            .Select(v => v.UserId)
            .FirstOrDefaultAsync(ct);

        if (vendorUserId != Guid.Empty)
        {
            var data = $"{{\"vendorId\":\"{vendorId}\",\"accountId\":\"{accountId}\",\"action\":\"verified\",\"targetUrl\":\"/profile/bank-accounts\"}}";
            await _notificationService.SendToUserAsync(
                vendorUserId,
                "اعتمدنا الحساب البنكي",
                "Bank account verified",
                "اعتمدنا حسابك البنكي بنجاح وهو جاهز للاستخدام.",
                "Your bank account has been verified and is ready to use.",
                NotificationTypes.VendorAccountUpdated,
                accountId,
                data,
                ct);

            await _oneSignalPushService.SendToExternalUserAsync(
                vendorUserId.ToString(),
                "اعتمدنا الحساب البنكي",
                "Bank account verified",
                "اعتمدنا حسابك البنكي بنجاح وهو جاهز للاستخدام.",
                "Your bank account has been verified and is ready to use.",
                NotificationTypes.VendorAccountUpdated,
                accountId,
                data,
                "/profile/bank-accounts",
                OneSignalPushProfile.Default,
                OneSignalApplicationTarget.VendorWeb,
                ct);
        }

        return Ok(new { Message = ApiLocalizedMessages.Resolve(HttpContext, "BANK_ACCOUNT_VERIFIED_SUCCESS") });
    }

    [HttpPost("{accountId:guid}/reject")]
    public async Task<ActionResult> RejectBankAccount(
        Guid vendorId, Guid accountId,
        [FromBody] RejectBankAccountRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Reason))
            throw new BadRequestException("REASON_REQUIRED", "Rejection reason is required.");

        await RequireVendorAsync(vendorId, ct);
        var account = await RequireAccountAsync(vendorId, accountId, ct);

        account.Reject(request.Reason.Trim());
        await _context.SaveChangesAsync(ct);

        // Notify vendor
        var vendorUserId = await _context.Vendors
            .AsNoTracking()
            .Where(v => v.Id == vendorId)
            .Select(v => v.UserId)
            .FirstOrDefaultAsync(ct);

        if (vendorUserId != Guid.Empty)
        {
            var data = $"{{\"vendorId\":\"{vendorId}\",\"accountId\":\"{accountId}\",\"action\":\"rejected\",\"reason\":\"{request.Reason.Trim()}\",\"targetUrl\":\"/profile/bank-accounts\"}}";
            await _notificationService.SendToUserAsync(
                vendorUserId,
                "رفضنا الحساب البنكي",
                "Bank account rejected",
                $"رفضنا حسابك البنكي. السبب: {request.Reason.Trim()}",
                $"Your bank account was rejected. Reason: {request.Reason.Trim()}",
                NotificationTypes.VendorAccountUpdated,
                accountId,
                data,
                ct);

            await _oneSignalPushService.SendToExternalUserAsync(
                vendorUserId.ToString(),
                "رفضنا الحساب البنكي",
                "Bank account rejected",
                $"رفضنا حسابك البنكي. السبب: {request.Reason.Trim()}",
                $"Your bank account was rejected. Reason: {request.Reason.Trim()}",
                NotificationTypes.VendorAccountUpdated,
                accountId,
                data,
                "/profile/bank-accounts",
                OneSignalPushProfile.Default,
                OneSignalApplicationTarget.VendorWeb,
                ct);
        }

        return Ok(new { Message = ApiLocalizedMessages.Resolve(HttpContext, "BANK_ACCOUNT_REJECTED_SUCCESS") });
    }

    [HttpPost("{accountId:guid}/set-primary")]
    public async Task<ActionResult> SetPrimary(Guid vendorId, Guid accountId, CancellationToken ct)
    {
        await RequireVendorAsync(vendorId, ct);
        var account = await RequireAccountAsync(vendorId, accountId, ct);

        // Unset all other primary accounts
        var existingPrimaries = await _context.VendorBankAccounts
            .Where(a => a.VendorId == vendorId && a.IsPrimary && a.Id != accountId)
            .ToListAsync(ct);

        foreach (var p in existingPrimaries)
            p.UnsetPrimary();

        account.SetAsPrimary();
        await _context.SaveChangesAsync(ct);

        return Ok(new { Message = ApiLocalizedMessages.Resolve(HttpContext, "BANK_ACCOUNT_PRIMARY_SUCCESS") });
    }

    private async Task RequireVendorAsync(Guid vendorId, CancellationToken ct)
    {
        var exists = await _context.Vendors.AnyAsync(v => v.Id == vendorId, ct);
        if (!exists) throw new NotFoundException("Vendor", vendorId);
    }

    private async Task<VendorBankAccount> RequireAccountAsync(Guid vendorId, Guid accountId, CancellationToken ct)
    {
        return await _context.VendorBankAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.VendorId == vendorId, ct)
            ?? throw new NotFoundException("VendorBankAccount", accountId);
    }
}

public record RejectBankAccountRequest(string Reason);
