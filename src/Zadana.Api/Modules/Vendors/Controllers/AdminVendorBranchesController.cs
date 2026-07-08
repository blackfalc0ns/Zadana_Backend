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
/// Admin endpoints for managing vendor branches
/// </summary>
[Route("api/admin/vendors/{vendorId:guid}/branches")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin Dashboard API")]
public class AdminVendorBranchesController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public AdminVendorBranchesController(
        IApplicationDbContext context,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService)
    {
        _context = context;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
    }

    [HttpGet]
    public async Task<ActionResult> GetBranches(Guid vendorId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        await RequireVendorAsync(vendorId, ct);

        var query = _context.VendorBranches.AsNoTracking().Where(b => b.VendorId == vendorId);
        if (!includeInactive) query = query.Where(b => b.IsActive);

        var branches = await query
            .OrderBy(b => b.Name)
            .Select(b => new
            {
                b.Id, b.Name, b.AddressLine, b.Latitude, b.Longitude,
                b.ContactPhone, b.DeliveryRadiusKm, b.IsActive, b.City, b.Region, b.CreatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(branches);
    }

    [HttpGet("{branchId:guid}")]
    public async Task<ActionResult> GetBranch(Guid vendorId, Guid branchId, CancellationToken ct = default)
    {
        await RequireVendorAsync(vendorId, ct);
        var branch = await _context.VendorBranches.AsNoTracking()
            .Include(b => b.OperatingHours)
            .FirstOrDefaultAsync(b => b.Id == branchId && b.VendorId == vendorId, ct)
            ?? throw new NotFoundException("VendorBranch", branchId);

        return Ok(new
        {
            branch.Id, branch.Name, branch.AddressLine, branch.Latitude, branch.Longitude,
            branch.ContactPhone, branch.DeliveryRadiusKm, branch.IsActive, branch.City, branch.Region, branch.CreatedAtUtc,
            OperatingHours = branch.OperatingHours.Select(h => new
            {
                h.DayOfWeek, h.OpenTime, h.CloseTime, h.IsClosed
            })
        });
    }

    [HttpPost]
    public async Task<ActionResult> CreateBranch(Guid vendorId, [FromBody] AdminBranchRequest request, CancellationToken ct = default)
    {
        await RequireVendorAsync(vendorId, ct);

        var branch = new VendorBranch(
            vendorId, request.Name, request.AddressLine,
            request.Latitude, request.Longitude,
            request.ContactPhone, request.DeliveryRadiusKm);

        _context.VendorBranches.Add(branch);
        await _context.SaveChangesAsync(ct);

        return Ok(new { branch.Id, branch.Name, Message = ApiLocalizedMessages.Resolve(HttpContext, "BRANCH_CREATED_SUCCESS") });
    }

    [HttpPut("{branchId:guid}")]
    public async Task<ActionResult> UpdateBranch(Guid vendorId, Guid branchId, [FromBody] AdminBranchRequest request, CancellationToken ct = default)
    {
        await RequireVendorAsync(vendorId, ct);
        var branch = await RequireBranchAsync(vendorId, branchId, ct);

        branch.Update(request.Name, request.AddressLine,
            request.Latitude, request.Longitude,
            request.ContactPhone, request.DeliveryRadiusKm);

        await _context.SaveChangesAsync(ct);
        return Ok(new { Message = ApiLocalizedMessages.Resolve(HttpContext, "BRANCH_UPDATED_SUCCESS") });
    }

    [HttpPost("{branchId:guid}/activate")]
    public async Task<ActionResult> ActivateBranch(Guid vendorId, Guid branchId, CancellationToken ct = default)
    {
        var branch = await RequireBranchAsync(vendorId, branchId, ct);
        branch.Activate();
        await _context.SaveChangesAsync(ct);

        // Notify vendor
        await NotifyVendorBranchStatusAsync(vendorId, branchId, branch.Name, "activated", ct);

        return Ok(new { Message = ApiLocalizedMessages.Resolve(HttpContext, "BRANCH_ACTIVATED_SUCCESS") });
    }

    [HttpPost("{branchId:guid}/deactivate")]
    public async Task<ActionResult> DeactivateBranch(Guid vendorId, Guid branchId, CancellationToken ct = default)
    {
        var branch = await RequireBranchAsync(vendorId, branchId, ct);
        branch.Deactivate();
        await _context.SaveChangesAsync(ct);

        // Notify vendor
        await NotifyVendorBranchStatusAsync(vendorId, branchId, branch.Name, "deactivated", ct);

        return Ok(new { Message = ApiLocalizedMessages.Resolve(HttpContext, "BRANCH_DEACTIVATED_SUCCESS") });
    }

    [HttpDelete("{branchId:guid}")]
    public async Task<ActionResult> DeleteBranch(Guid vendorId, Guid branchId, CancellationToken ct = default)
    {
        var branch = await RequireBranchAsync(vendorId, branchId, ct);
        _context.VendorBranches.Remove(branch);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task RequireVendorAsync(Guid vendorId, CancellationToken ct)
    {
        if (!await _context.Vendors.AnyAsync(v => v.Id == vendorId, ct))
            throw new NotFoundException("Vendor", vendorId);
    }

    private async Task<VendorBranch> RequireBranchAsync(Guid vendorId, Guid branchId, CancellationToken ct)
    {
        return await _context.VendorBranches
            .FirstOrDefaultAsync(b => b.Id == branchId && b.VendorId == vendorId, ct)
            ?? throw new NotFoundException("VendorBranch", branchId);
    }

    private async Task NotifyVendorBranchStatusAsync(
        Guid vendorId, Guid branchId, string branchName, string action, CancellationToken ct)
    {
        var vendorUserId = await _context.Vendors
            .AsNoTracking()
            .Where(v => v.Id == vendorId)
            .Select(v => v.UserId)
            .FirstOrDefaultAsync(ct);

        if (vendorUserId == Guid.Empty) return;

        var isActivated = action == "activated";
        var titleAr = isActivated ? "فعّلنا الفرع" : "عطّلنا الفرع";
        var titleEn = isActivated ? "Branch activated" : "Branch deactivated";
        var bodyAr = isActivated
            ? $"فعّلنا فرع {branchName} بنجاح وهو جاهز لاستقبال الطلبات."
            : $"عطّلنا فرع {branchName} مؤقتا وما راح يستقبل طلبات حتى إشعار آخر.";
        var bodyEn = isActivated
            ? $"Branch '{branchName}' has been activated and is ready to accept orders."
            : $"Branch '{branchName}' has been deactivated and will not accept orders until further notice.";
        var data = $"{{\"vendorId\":\"{vendorId}\",\"branchId\":\"{branchId}\",\"branchName\":\"{branchName}\",\"action\":\"{action}\",\"targetUrl\":\"/branches\"}}";

        await _notificationService.SendToUserAsync(
            vendorUserId,
            titleAr, titleEn, bodyAr, bodyEn,
            NotificationTypes.VendorAccountUpdated,
            branchId, data, ct);

        await _oneSignalPushService.SendToExternalUserAsync(
            vendorUserId.ToString(),
            titleAr, titleEn, bodyAr, bodyEn,
            NotificationTypes.VendorAccountUpdated,
            branchId, data, "/branches",
            OneSignalPushProfile.Default,
            OneSignalApplicationTarget.VendorWeb,
            ct);
    }
}

public record AdminBranchRequest(
    string Name, string AddressLine,
    decimal Latitude, decimal Longitude,
    string ContactPhone, decimal DeliveryRadiusKm);
