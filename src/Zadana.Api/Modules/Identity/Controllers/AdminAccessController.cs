using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Zadana.Api.Controllers;
using Zadana.Api.Authorization;
using Zadana.Domain.Modules.Identity.Constants;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using Zadana.Application.Modules.Identity.Queries.GetRoles;
using Zadana.Application.Modules.Identity.Queries.GetAdminUsers;
using Zadana.Application.Modules.Identity.Queries.GetUserEffectiveAccess;
using Zadana.Application.Modules.Identity.Commands.CreateRole;
using Zadana.Application.Modules.Identity.Commands.UpdateRole;
using Zadana.Application.Modules.Identity.Commands.DeleteRole;
using Zadana.Application.Modules.Identity.Commands.UpdateUserScope;
using Zadana.Application.Modules.Identity.Commands.UpdateUserOverrides;
using Zadana.Application.Modules.Identity.Commands.AdminAccessUsers;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Identity.Services;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.SharedKernel.Serialization;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/admin/access")]
[Authorize(Policy = "AdminOnly")]
public class AdminAccessController(
    IMediator mediator,
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IIdentityAccountService identityAccountService,
    IEmailVerificationSender emailVerificationSender,
    IAccessAuditService auditService,
    INotificationService notificationService,
    IOneSignalPushService oneSignalPushService,
    ILogger<AdminAccessController> logger) : ApiControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    [HttpGet("permissions")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetPermissions(CancellationToken cancellationToken)
    {
        var permissions = await dbContext.PermissionDefinitions
            .AsNoTracking()
            .OrderBy(x => x.PanelScope)
            .ThenBy(x => x.Domain)
            .ThenBy(x => x.Action)
            .Select(x => new PermissionDefinitionDto(
                x.Id,
                x.Key,
                x.Name,
                x.Domain,
                x.Action,
                x.PanelScope,
                x.Description,
                x.IsSensitive))
            .ToListAsync(cancellationToken);

        return Ok(permissions);
    }

    [HttpGet("roles")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetRoles()
    {
        var result = await mediator.Send(new GetRolesQuery());
        return Ok(result);
    }

    [HttpGet("roles/{id}")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetRole(Guid id, CancellationToken cancellationToken)
    {
        var role = await dbContext.RoleDefinitions
            .AsNoTracking()
            .Include(item => item.RolePermissions)
            .ThenInclude(item => item.PermissionDefinition)
            .Include(item => item.UserAccessScopes)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (role is null)
        {
            return NotFound();
        }

        return Ok(new RoleDefinitionDto(
            role.Id,
            role.Code,
            role.Name,
            role.Description,
            role.IsSystem,
            role.IsActive,
            role.IdentityRole,
            role.PanelScope,
            role.RolePermissions
                .Select(item => item.PermissionDefinition.Key)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            role.UserAccessScopes.Count));
    }

    [HttpPost("roles")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessCreate)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleCommand command)
    {
        var result = await mediator.Send(command);
        return Ok(result);
    }

    [HttpPut("roles/{id}")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessEdit)]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleCommand command)
    {
        if (id != command.Id) return BadRequest();
        var result = await mediator.Send(command);
        return Ok(result);
    }

    [HttpDelete("roles/{id}")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessEdit)]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        await mediator.Send(new DeleteRoleCommand(id));
        return NoContent();
    }

    [HttpGet("users")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? roleDefinitionId = null,
        [FromQuery] PanelScope? panelScope = null)
    {
        var result = await mediator.Send(new GetAdminUsersQuery(
            pageNumber,
            pageSize,
            search,
            status,
            roleDefinitionId,
            panelScope));
        return Ok(result);
    }

    [HttpGet("users/{id}")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var result = await mediator.Send(new GetAdminUserByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("users")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessCreate)]
    public async Task<IActionResult> CreateUser([FromBody] CreateAdminAccessUserRequest request)
    {
        var result = await mediator.Send(new CreateAdminAccessUserCommand(
            request.FullName,
            request.Email,
            request.Phone,
            request.Password,
            request.RoleDefinitionId,
            request.PanelScope,
            request.ScopeType,
            request.ScopeEntityId,
            request.Department,
            request.Team,
            request.Notes));

        return Ok(result);
    }

    [HttpPut("users/{id}")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessEdit)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateAdminAccessUserRequest request)
    {
        var result = await mediator.Send(new UpdateAdminAccessUserCommand(
            id,
            request.FullName,
            request.Email,
            request.Phone,
            request.RoleDefinitionId,
            request.PanelScope,
            request.ScopeType,
            request.ScopeEntityId,
            request.Department,
            request.Team,
            request.Status,
            request.Notes,
            request.GrantedPermissions,
            request.RevokedPermissions,
            request.Communication));

        return Ok(result);
    }

    [HttpPost("users/{id}/temporary-password")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessEdit)]
    public async Task<IActionResult> ResetTemporaryPassword(Guid id, [FromBody] ResetTemporaryPasswordRequest request)
    {
        var result = await mediator.Send(new ResetAdminAccessUserTemporaryPasswordCommand(
            id,
            request.TemporaryPassword));

        return Ok(result);
    }

    [HttpGet("users/{id}/audit")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetUserAudit(Guid id, CancellationToken cancellationToken)
    {
        var result = await (
                from log in dbContext.AccessAuditLogs.AsNoTracking()
                join actor in dbContext.Users.AsNoTracking()
                    on log.ActorUserId equals (Guid?)actor.Id into actorJoin
                from actor in actorJoin.DefaultIfEmpty()
                where log.TargetUserId == id
                orderby log.CreatedAtUtc descending
                select new AccessAuditLogDto(
                    log.Id,
                    log.ActorUserId,
                    actor != null ? actor.FullName : null,
                    actor != null ? actor.Email : null,
                    log.TargetUserId,
                    log.Action,
                    log.Summary,
                    log.BeforeJson,
                    log.AfterJson,
                    SaudiTime.ToSaudi(log.CreatedAtUtc).ToString("O"),
                    log.IpAddress,
                    log.UserAgent))
            .Take(100)
            .ToListAsync(cancellationToken);

        return Ok(result);
    }

    [HttpPut("users/{id}/scope")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessEdit)]
    public async Task<IActionResult> UpdateUserScope(Guid id, [FromBody] UpdateUserScopeRequest request)
    {
        await mediator.Send(new UpdateUserScopeCommand(
            UserId: id,
            RoleDefinitionId: request.RoleDefinitionId,
            PanelScope: request.PanelScope,
            ScopeType: request.ScopeType,
            ScopeEntityId: request.ScopeEntityId,
            Notes: request.Notes
        ));
        return Ok();
    }

    [HttpPut("users/{id}/overrides")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessEdit)]
    public async Task<IActionResult> UpdateUserOverrides(Guid id, [FromBody] UpdateUserOverridesRequest request)
    {
        await mediator.Send(new UpdateUserOverridesCommand(
            UserId: id,
            GrantedPermissions: request.GrantedPermissions,
            RevokedPermissions: request.RevokedPermissions
        ));
        return Ok();
    }

    [HttpGet("users/{id}/effective-access")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetUserEffectiveAccess(Guid id)
    {
        var result = await mediator.Send(new GetUserEffectiveAccessQuery(id));
        return Ok(result);
    }

    [HttpGet("approvals")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessApprove)]
    public async Task<IActionResult> GetApprovals(
        [FromQuery] AccessApprovalStatus? status = AccessApprovalStatus.Pending,
        [FromQuery] Guid? requestedByUserId = null,
        [FromQuery] Guid? targetUserId = null,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 250);

        var query = dbContext.AccessApprovalRequests.AsNoTracking();
        if (status.HasValue)
        {
            query = query.Where(request => request.Status == status.Value);
        }

        if (requestedByUserId.HasValue)
        {
            query = query.Where(request => request.RequestedByUserId == requestedByUserId.Value);
        }

        if (targetUserId.HasValue)
        {
            query = query.Where(request => request.TargetUserId == targetUserId.Value);
        }

        var result = await ProjectApprovalRequests(query.OrderByDescending(request => request.CreatedAtUtc))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(await AttachReviewDetailsAsync(result, cancellationToken));
    }

    [HttpPost("approvals/{id}/approve")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessApprove)]
    public async Task<IActionResult> ApproveRequest(
        Guid id,
        [FromBody] AccessApprovalDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        var approval = await dbContext.AccessApprovalRequests
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (approval is null)
        {
            return NotFound();
        }

        EnsureCurrentUserCanDecideApproval(approval);
        var before = SnapshotApproval(approval);
        approval.Approve(currentUserService.UserId!.Value, request?.Note);
        IReadOnlyList<DriverDocumentType>? approvedDriverDocuments = null;
        if (ProfileChangeApprovalActions.IsProfileChange(approval.Action))
        {
            approvedDriverDocuments = await ApplyApprovedProfileChangeAsync(approval, cancellationToken);
            approval.Consume();
        }
        auditService.Add(
            approval.TargetUserId ?? approval.RequestedByUserId,
            "access-approval-approved",
            $"Access approval request {approval.Action} was approved.",
            before,
            SnapshotApproval(approval));

        await dbContext.SaveChangesAsync(cancellationToken);

        if (approvedDriverDocuments is { Count: > 0 })
        {
            var payload = DeserializePayload<DriverDocumentsProfileChangePayload>(approval);
            await NotifyDriverDocumentsApprovedAsync(payload.DriverId, approvedDriverDocuments, cancellationToken);
        }
        else if (approval.Action == ProfileChangeApprovalActions.DriverProfilePersonal)
        {
            var payload = DeserializePayload<DriverPersonalProfileChangePayload>(approval);
            await NotifyDriverProfileSectionApprovedAsync(payload.DriverId, "personal", cancellationToken);
        }
        else if (approval.Action == ProfileChangeApprovalActions.DriverProfileVehicle)
        {
            var payload = DeserializePayload<DriverVehicleProfileChangePayload>(approval);
            await NotifyDriverProfileSectionApprovedAsync(payload.DriverId, "vehicle", cancellationToken);
        }

        return Ok(await ProjectApprovalRequestAsync(id, cancellationToken));
    }

    [HttpPost("approvals/{id}/reject")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessApprove)]
    public async Task<IActionResult> RejectRequest(
        Guid id,
        [FromBody] AccessApprovalDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        var approval = await dbContext.AccessApprovalRequests
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (approval is null)
        {
            return NotFound();
        }

        EnsureCurrentUserCanDecideApproval(approval);
        var before = SnapshotApproval(approval);
        approval.Reject(currentUserService.UserId!.Value, request?.Note);
        auditService.Add(
            approval.TargetUserId ?? approval.RequestedByUserId,
            "access-approval-rejected",
            $"Access approval request {approval.Action} was rejected.",
            before,
            SnapshotApproval(approval));

        await dbContext.SaveChangesAsync(cancellationToken);

        if (approval.Action == ProfileChangeApprovalActions.DriverProfileDocuments)
        {
            var payload = DeserializePayload<DriverDocumentsProfileChangePayload>(approval);
            var reason = string.IsNullOrWhiteSpace(request?.Note)
                ? "رفضنا طلب تحديث المستندات."
                : request.Note.Trim();
            await NotifyDriverDocumentsRejectedAsync(payload, reason, cancellationToken);
        }
        else if (approval.Action == ProfileChangeApprovalActions.DriverProfilePersonal)
        {
            var payload = DeserializePayload<DriverPersonalProfileChangePayload>(approval);
            var reason = string.IsNullOrWhiteSpace(request?.Note)
                ? "رفضنا طلب تحديث البيانات الشخصية."
                : request.Note.Trim();
            await NotifyDriverProfileSectionRejectedAsync(payload.DriverId, "personal", reason, cancellationToken);
        }
        else if (approval.Action == ProfileChangeApprovalActions.DriverProfileVehicle)
        {
            var payload = DeserializePayload<DriverVehicleProfileChangePayload>(approval);
            var reason = string.IsNullOrWhiteSpace(request?.Note)
                ? "رفضنا طلب تحديث بيانات المركبة."
                : request.Note.Trim();
            await NotifyDriverProfileSectionRejectedAsync(payload.DriverId, "vehicle", reason, cancellationToken);
        }

        return Ok(await ProjectApprovalRequestAsync(id, cancellationToken));
    }

    [HttpGet("audit")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetAuditEvents(
        [FromQuery] Guid? targetUserId = null,
        [FromQuery] Guid? actorUserId = null,
        [FromQuery] string? action = null,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 250);

        var query = dbContext.AccessAuditLogs.AsNoTracking();
        if (targetUserId.HasValue)
        {
            query = query.Where(log => log.TargetUserId == targetUserId.Value);
        }

        if (actorUserId.HasValue)
        {
            query = query.Where(log => log.ActorUserId == actorUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim();
            query = query.Where(log => log.Action == normalizedAction);
        }

        var result = await (
                from log in query
                join actor in dbContext.Users.AsNoTracking()
                    on log.ActorUserId equals (Guid?)actor.Id into actorJoin
                from actor in actorJoin.DefaultIfEmpty()
                orderby log.CreatedAtUtc descending
                select new AccessAuditLogDto(
                    log.Id,
                    log.ActorUserId,
                    actor != null ? actor.FullName : null,
                    actor != null ? actor.Email : null,
                    log.TargetUserId,
                    log.Action,
                    log.Summary,
                    log.BeforeJson,
                    log.AfterJson,
                    SaudiTime.ToSaudi(log.CreatedAtUtc).ToString("O"),
                    log.IpAddress,
                    log.UserAgent))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(result);
    }

    private void EnsureCurrentUserCanDecideApproval(AccessApprovalRequest approval)
    {
        if (!currentUserService.UserId.HasValue)
        {
            throw new BusinessRuleException("APPROVER_REQUIRED", "An authenticated approver is required.");
        }

        if (approval.RequestedByUserId == currentUserService.UserId.Value)
        {
            throw new BusinessRuleException("SELF_APPROVAL_BLOCKED", "You cannot approve or reject your own access request.");
        }
    }

    private async Task<IReadOnlyList<DriverDocumentType>?> ApplyApprovedProfileChangeAsync(
        AccessApprovalRequest approval,
        CancellationToken cancellationToken)
    {
        switch (approval.Action)
        {
            case ProfileChangeApprovalActions.VendorProfileBasic:
                await ApplyVendorBasicChangeAsync(
                    DeserializePayload<VendorBasicProfileChangePayload>(approval),
                    cancellationToken);
                return null;

            case ProfileChangeApprovalActions.VendorProfileStore:
                await ApplyVendorStoreChangeAsync(
                    DeserializePayload<VendorStoreProfileChangePayload>(approval),
                    cancellationToken);
                return null;

            case ProfileChangeApprovalActions.VendorProfileOwner:
                await ApplyVendorOwnerChangeAsync(
                    DeserializePayload<VendorOwnerProfileChangePayload>(approval),
                    cancellationToken);
                return null;

            case ProfileChangeApprovalActions.VendorProfileLegal:
                await ApplyVendorLegalChangeAsync(
                    DeserializePayload<VendorLegalProfileChangePayload>(approval),
                    cancellationToken);
                return null;

            case ProfileChangeApprovalActions.VendorProfileBanking:
                await ApplyVendorBankingChangeAsync(
                    DeserializePayload<VendorBankingProfileChangePayload>(approval),
                    cancellationToken);
                return null;

            case ProfileChangeApprovalActions.DriverProfilePersonal:
                await ApplyDriverPersonalChangeAsync(
                    DeserializePayload<DriverPersonalProfileChangePayload>(approval),
                    cancellationToken);
                return null;

            case ProfileChangeApprovalActions.DriverProfileVehicle:
                await ApplyDriverVehicleChangeAsync(
                    DeserializePayload<DriverVehicleProfileChangePayload>(approval),
                    cancellationToken);
                return null;

            case ProfileChangeApprovalActions.DriverProfileDocuments:
                return await ApplyDriverDocumentsChangeAsync(
                    DeserializePayload<DriverDocumentsProfileChangePayload>(approval),
                    cancellationToken);

            case ProfileChangeApprovalActions.DriverPayoutMethodCreate:
                await ApplyDriverPayoutMethodCreateAsync(
                    DeserializePayload<DriverPayoutMethodCreatePayload>(approval),
                    cancellationToken);
                return null;

            case ProfileChangeApprovalActions.DriverPayoutMethodUpdate:
                await ApplyDriverPayoutMethodUpdateAsync(
                    DeserializePayload<DriverPayoutMethodUpdatePayload>(approval),
                    cancellationToken);
                return null;

            case ProfileChangeApprovalActions.DriverPayoutMethodMakePrimary:
                await ApplyDriverPayoutMethodMakePrimaryAsync(
                    DeserializePayload<DriverPayoutMethodMakePrimaryPayload>(approval),
                    cancellationToken);
                return null;

            case ProfileChangeApprovalActions.DriverPayoutMethodDelete:
                await ApplyDriverPayoutMethodDeleteAsync(
                    DeserializePayload<DriverPayoutMethodDeletePayload>(approval),
                    cancellationToken);
                return null;

            default:
                throw new BusinessRuleException(
                    "UNSUPPORTED_PROFILE_APPROVAL_ACTION",
                    $"Unsupported profile approval action: {approval.Action}");
        }
    }

    private async Task ApplyVendorStoreChangeAsync(
        VendorStoreProfileChangePayload payload,
        CancellationToken cancellationToken)
    {
        var vendor = await dbContext.Vendors
            .Include(item => item.ProfileReviewItems)
            .FirstOrDefaultAsync(item => item.Id == payload.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", payload.VendorId);

        vendor.UpdateStore(
            vendor.BusinessNameAr,
            vendor.BusinessNameEn,
            vendor.BusinessType,
            vendor.ContactEmail,
            vendor.ContactPhone,
            vendor.DescriptionAr,
            vendor.DescriptionEn,
            vendor.LogoUrl,
            payload.CommercialRegisterDocumentUrl,
            vendor.Region,
            vendor.City,
            vendor.NationalAddress,
            payload.CommercialRegistrationNumber);
    }

    private async Task ApplyVendorBasicChangeAsync(
        VendorBasicProfileChangePayload payload,
        CancellationToken cancellationToken)
    {
        var vendor = await dbContext.Vendors
            .FirstOrDefaultAsync(item => item.Id == payload.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", payload.VendorId);

        vendor.UpdateProfile(
            vendor.BusinessNameAr,
            vendor.BusinessNameEn,
            vendor.BusinessType,
            vendor.ContactEmail,
            vendor.ContactPhone,
            payload.TaxId);
    }

    private async Task ApplyVendorOwnerChangeAsync(
        VendorOwnerProfileChangePayload payload,
        CancellationToken cancellationToken)
    {
        var vendor = await dbContext.Vendors
            .Include(item => item.ProfileReviewItems)
            .FirstOrDefaultAsync(item => item.Id == payload.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", payload.VendorId);

        vendor.UpdateOwner(
            payload.OwnerName,
            payload.OwnerEmail,
            payload.OwnerPhone,
            payload.IdNumber,
            payload.Nationality);

        var updateIdentityResult = await identityAccountService.UpdateProfileAsync(
            vendor.UserId,
            payload.OwnerName,
            payload.OwnerEmail,
            payload.OwnerPhone,
            cancellationToken);

        if (!updateIdentityResult.Succeeded)
        {
            throw new BusinessRuleException(
                "IDENTITY_UPDATE_FAILED",
                string.Join(", ", updateIdentityResult.Errors ?? []));
        }

        if (updateIdentityResult.EmailChanged)
        {
            await emailVerificationSender.SendAsync(vendor.UserId, cancellationToken);
        }
    }

    private async Task ApplyVendorLegalChangeAsync(
        VendorLegalProfileChangePayload payload,
        CancellationToken cancellationToken)
    {
        var vendor = await dbContext.Vendors
            .Include(item => item.DocumentReviews)
            .Include(item => item.ProfileReviewItems)
            .FirstOrDefaultAsync(item => item.Id == payload.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", payload.VendorId);

        var resetDocuments = ResolveReuploadedRejectedDocuments(
            vendor.CommercialRegisterDocumentUrl,
            payload.CommercialRegisterDocumentUrl,
            vendor.TaxDocumentUrl,
            payload.TaxDocumentUrl,
            vendor.LicenseDocumentUrl,
            payload.LicenseDocumentUrl);

        vendor.UpdateLegal(
            payload.CommercialRegistrationNumber,
            payload.CommercialRegistrationExpiryDate,
            payload.TaxId,
            payload.LicenseNumber,
            payload.CommercialRegisterDocumentUrl,
            payload.TaxDocumentUrl,
            payload.LicenseDocumentUrl);

        foreach (var documentType in resetDocuments)
        {
            var review = vendor.DocumentReviews.FirstOrDefault(item => item.Type == documentType);
            if (review?.Decision == VendorDocumentReviewDecision.Rejected)
            {
                review.ResetToPending();
            }
        }
    }

    private async Task ApplyVendorBankingChangeAsync(
        VendorBankingProfileChangePayload payload,
        CancellationToken cancellationToken)
    {
        var vendor = await dbContext.Vendors
            .Include(item => item.BankAccounts)
            .Include(item => item.ProfileReviewItems)
            .FirstOrDefaultAsync(item => item.Id == payload.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", payload.VendorId);

        // A payout-day choice is now a self-service preference saved immediately.
        // Do not let an older pending banking approval overwrite a newer choice.
        vendor.UpdateBanking(payload.PayoutCycle);

        var primaryAccount = vendor.BankAccounts
            .FirstOrDefault(account => account.IsPrimary)
            ?? vendor.BankAccounts
                .OrderByDescending(account => account.CreatedAtUtc)
                .FirstOrDefault();

        foreach (var account in vendor.BankAccounts)
        {
            account.UnsetPrimary();
        }

        if (primaryAccount is null)
        {
            primaryAccount = new VendorBankAccount(
                vendor.Id,
                payload.BankName,
                payload.AccountHolderName,
                payload.Iban,
                payload.SwiftCode);

            primaryAccount.MarkAsPreferredForSetup();
            dbContext.VendorBankAccounts.Add(primaryAccount);
        }
        else
        {
            primaryAccount.UpdateDetails(
                payload.BankName,
                payload.AccountHolderName,
                payload.Iban,
                payload.SwiftCode);
            primaryAccount.MarkAsPreferredForSetup();
        }
    }

    private async Task ApplyDriverPersonalChangeAsync(
        DriverPersonalProfileChangePayload payload,
        CancellationToken cancellationToken)
    {
        var driver = await dbContext.Drivers
            .Include(item => item.User)
            .Include(item => item.DocumentReviews)
            .FirstOrDefaultAsync(item => item.Id == payload.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", payload.DriverId);

        var updateResult = await identityAccountService.UpdateProfileAsync(
            driver.UserId,
            payload.FullName,
            payload.Email,
            payload.Phone,
            cancellationToken);

        if (!updateResult.Succeeded)
        {
            throw new BusinessRuleException(
                "IDENTITY_PROFILE_UPDATE_FAILED",
                string.Join(", ", updateResult.Errors ?? []));
        }

        if (updateResult.EmailChanged)
        {
            await emailVerificationSender.SendAsync(driver.UserId, cancellationToken);
        }

        driver.UpdateAddress(payload.Address);
        driver.RefreshProfileReviewState(
            HasRequiredDriverProfileData(driver),
            sensitiveChange: false,
            note: "Personal profile change approved by admin.");
    }

    private async Task ApplyDriverVehicleChangeAsync(
        DriverVehicleProfileChangePayload payload,
        CancellationToken cancellationToken)
    {
        var driver = await dbContext.Drivers
            .Include(item => item.User)
            .Include(item => item.DocumentReviews)
            .FirstOrDefaultAsync(item => item.Id == payload.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", payload.DriverId);

        var reviewerId = currentUserService.UserId
            ?? throw new BusinessRuleException("ADMIN_REVIEWER_REQUIRED", "An authenticated admin reviewer is required.");
        var reviewerName = await ResolveDriverComplianceReviewerNameAsync(reviewerId, cancellationToken);

        DriverVehicleType? parsedVehicleType = null;
        if (!string.IsNullOrWhiteSpace(payload.VehicleType))
        {
            if (!DriverVehicleTypeMapper.TryParse(payload.VehicleType, out var resolvedVehicleType))
            {
                throw new BusinessRuleException("INVALID_VEHICLE_TYPE", "Unsupported vehicle type.");
            }

            parsedVehicleType = resolvedVehicleType;
        }

        var nationalIdChanged =
            HasChanged(driver.NationalId, payload.NationalId) ||
            HasChanged(driver.NationalIdExpiryDate, payload.NationalIdExpiryDate);
        var driverLicenseChanged =
            HasChanged(driver.LicenseNumber, payload.LicenseNumber) ||
            HasChanged(driver.DriverLicenseExpiryDate, payload.DriverLicenseExpiryDate);
        var vehicleLicenseChanged =
            HasChanged(driver.VehicleLicenseNumber, payload.VehicleLicenseNumber) ||
            HasChanged(driver.VehicleLicenseExpiryDate, payload.VehicleLicenseExpiryDate);

        driver.UpdateDetails(
            parsedVehicleType,
            payload.NationalId,
            payload.LicenseNumber,
            payload.NationalIdExpiryDate,
            payload.DriverLicenseExpiryDate,
            payload.VehicleLicenseNumber,
            payload.VehicleLicenseExpiryDate);

        if (nationalIdChanged)
        {
            ApproveDriverDocumentReviewIfReady(driver, DriverDocumentType.NationalId, reviewerId, reviewerName);
        }

        if (driverLicenseChanged)
        {
            ApproveDriverDocumentReviewIfReady(driver, DriverDocumentType.DriverLicense, reviewerId, reviewerName);
        }

        if (vehicleLicenseChanged)
        {
            ApproveDriverDocumentReviewIfReady(driver, DriverDocumentType.VehicleLicense, reviewerId, reviewerName);
        }

        driver.UpdateServiceArea(payload.Region, payload.City);
        driver.RefreshProfileReviewState(
            HasRequiredDriverProfileData(driver),
            sensitiveChange: false,
            note: "Vehicle profile change approved by admin.");
    }

    private async Task<IReadOnlyList<DriverDocumentType>> ApplyDriverDocumentsChangeAsync(
        DriverDocumentsProfileChangePayload payload,
        CancellationToken cancellationToken)
    {
        var driver = await dbContext.Drivers
            .Include(item => item.User)
            .Include(item => item.DocumentReviews)
            .FirstOrDefaultAsync(item => item.Id == payload.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", payload.DriverId);

        var nationalIdChanged =
            HasChanged(driver.NationalIdFrontImageUrl, payload.NationalIdFrontImageUrl) ||
            HasChanged(driver.NationalIdBackImageUrl, payload.NationalIdBackImageUrl);
        var driverLicenseChanged = HasChanged(driver.LicenseImageUrl, payload.LicenseImageUrl);
        var vehicleLicenseChanged = HasChanged(driver.VehicleImageUrl, payload.VehicleImageUrl);

        driver.UpdateDocuments(
            payload.NationalIdFrontImageUrl,
            payload.NationalIdBackImageUrl,
            payload.LicenseImageUrl,
            payload.VehicleImageUrl,
            payload.PersonalPhotoUrl);

        var reviewerId = currentUserService.UserId
            ?? throw new BusinessRuleException("ADMIN_REVIEWER_REQUIRED", "An authenticated admin reviewer is required.");
        var reviewerName = await ResolveDriverComplianceReviewerNameAsync(reviewerId, cancellationToken);
        var approvedDocumentTypes = new List<DriverDocumentType>();

        if (nationalIdChanged)
        {
            ApproveDriverDocumentReviewIfReady(driver, DriverDocumentType.NationalId, reviewerId, reviewerName);
            approvedDocumentTypes.Add(DriverDocumentType.NationalId);
        }

        if (driverLicenseChanged)
        {
            ApproveDriverDocumentReviewIfReady(driver, DriverDocumentType.DriverLicense, reviewerId, reviewerName);
            approvedDocumentTypes.Add(DriverDocumentType.DriverLicense);
        }

        if (vehicleLicenseChanged)
        {
            ApproveDriverDocumentReviewIfReady(driver, DriverDocumentType.VehicleLicense, reviewerId, reviewerName);
            approvedDocumentTypes.Add(DriverDocumentType.VehicleLicense);
        }

        driver.RefreshProfileReviewState(
            HasRequiredDriverProfileData(driver),
            sensitiveChange: false,
            note: "Documents approved for profile update by admin.");

        return approvedDocumentTypes;
    }

    private async Task ApplyDriverPayoutMethodCreateAsync(
        DriverPayoutMethodCreatePayload payload,
        CancellationToken cancellationToken)
    {
        var driverExists = await dbContext.Drivers
            .AnyAsync(item => item.Id == payload.DriverId, cancellationToken);

        if (!driverExists)
        {
            throw new NotFoundException("Driver", payload.DriverId);
        }

        var methodType = ParseDriverPayoutMethodType(payload.Type);
        EnsureSupportedDriverBankPayoutMethod(methodType, payload.AccountIdentifier);

        var existingMethods = await dbContext.DriverPayoutMethods
            .Where(item => item.DriverId == payload.DriverId)
            .ToListAsync(cancellationToken);

        var shouldBePrimary = payload.IsPrimary || existingMethods.Count == 0;
        if (shouldBePrimary)
        {
            foreach (var method in existingMethods.Where(item => item.IsPrimary))
            {
                method.UnsetPrimary();
            }
        }

        dbContext.DriverPayoutMethods.Add(new DriverPayoutMethod(
            payload.DriverId,
            methodType,
            payload.AccountHolderName,
            payload.AccountIdentifier,
            payload.ProviderName,
            shouldBePrimary));
    }

    private async Task ApplyDriverPayoutMethodUpdateAsync(
        DriverPayoutMethodUpdatePayload payload,
        CancellationToken cancellationToken)
    {
        var payoutMethod = await dbContext.DriverPayoutMethods
            .FirstOrDefaultAsync(
                item => item.Id == payload.PayoutMethodId && item.DriverId == payload.DriverId,
                cancellationToken)
            ?? throw new NotFoundException("DriverPayoutMethod", payload.PayoutMethodId);

        var methodType = ParseDriverPayoutMethodType(payload.Type);
        EnsureSupportedDriverBankPayoutMethod(methodType, payload.AccountIdentifier);

        payoutMethod.UpdateDetails(
            methodType,
            payload.AccountHolderName,
            payload.AccountIdentifier,
            payload.ProviderName);
    }

    private async Task ApplyDriverPayoutMethodMakePrimaryAsync(
        DriverPayoutMethodMakePrimaryPayload payload,
        CancellationToken cancellationToken)
    {
        var methods = await dbContext.DriverPayoutMethods
            .Where(item => item.DriverId == payload.DriverId)
            .ToListAsync(cancellationToken);

        var payoutMethod = methods.FirstOrDefault(item => item.Id == payload.PayoutMethodId)
            ?? throw new NotFoundException("DriverPayoutMethod", payload.PayoutMethodId);

        EnsureSupportedDriverBankPayoutMethod(payoutMethod.MethodType, payoutMethod.AccountIdentifier);

        foreach (var method in methods)
        {
            method.UnsetPrimary();
        }

        payoutMethod.SetPrimary();
    }

    private async Task ApplyDriverPayoutMethodDeleteAsync(
        DriverPayoutMethodDeletePayload payload,
        CancellationToken cancellationToken)
    {
        var methods = await dbContext.DriverPayoutMethods
            .Where(item => item.DriverId == payload.DriverId)
            .ToListAsync(cancellationToken);

        var payoutMethod = methods.FirstOrDefault(item => item.Id == payload.PayoutMethodId)
            ?? throw new NotFoundException("DriverPayoutMethod", payload.PayoutMethodId);

        var hasWithdrawalHistory = await dbContext.DriverWithdrawalRequests
            .AnyAsync(item => item.DriverPayoutMethodId == payload.PayoutMethodId, cancellationToken);

        if (hasWithdrawalHistory)
        {
            throw new BusinessRuleException(
                "DRIVER_PAYOUT_METHOD_IN_USE",
                "This payout method cannot be deleted because it is linked to withdrawal requests.");
        }

        var isPrimary = payoutMethod.IsPrimary;
        dbContext.DriverPayoutMethods.Remove(payoutMethod);

        if (isPrimary)
        {
            methods
                .Where(item => item.Id != payload.PayoutMethodId)
                .OrderByDescending(item => item.CreatedAtUtc)
                .FirstOrDefault()
                ?.SetPrimary();
        }
    }

    private static TPayload DeserializePayload<TPayload>(AccessApprovalRequest approval)
    {
        return JsonSerializer.Deserialize<TPayload>(approval.PayloadJson, JsonOptions)
            ?? throw new BusinessRuleException(
                "INVALID_APPROVAL_PAYLOAD",
                $"Approval payload for {approval.Action} is invalid.");
    }

    private static IReadOnlyList<VendorDocumentType> ResolveReuploadedRejectedDocuments(
        string? currentCommercialUrl,
        string? nextCommercialUrl,
        string? currentTaxUrl,
        string? nextTaxUrl,
        string? currentLicenseUrl,
        string? nextLicenseUrl)
    {
        var changed = new List<VendorDocumentType>();

        if (HasChanged(currentCommercialUrl, nextCommercialUrl))
        {
            changed.Add(VendorDocumentType.Commercial);
        }

        if (HasChanged(currentTaxUrl, nextTaxUrl))
        {
            changed.Add(VendorDocumentType.Tax);
        }

        if (HasChanged(currentLicenseUrl, nextLicenseUrl))
        {
            changed.Add(VendorDocumentType.License);
        }

        return changed;
    }

    private static bool HasChanged(string? currentValue, string? nextValue) =>
        !string.IsNullOrWhiteSpace(nextValue) &&
        !string.Equals(currentValue?.Trim(), nextValue.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool HasChanged(DateTime? currentValue, DateTime? nextValue) =>
        nextValue.HasValue &&
        (!currentValue.HasValue || currentValue.Value.Date != nextValue.Value.Date);

    private static bool HasRequiredDriverProfileData(Domain.Modules.Delivery.Entities.Driver driver) =>
        driver.VehicleType is not null &&
        !string.IsNullOrWhiteSpace(driver.NationalId) &&
        !string.IsNullOrWhiteSpace(driver.LicenseNumber) &&
        !string.IsNullOrWhiteSpace(driver.VehicleLicenseNumber) &&
        !string.IsNullOrWhiteSpace(driver.Address) &&
        !string.IsNullOrWhiteSpace(driver.PersonalPhotoUrl) &&
        !string.IsNullOrWhiteSpace(driver.NationalIdFrontImageUrl) &&
        !string.IsNullOrWhiteSpace(driver.NationalIdBackImageUrl) &&
        !string.IsNullOrWhiteSpace(driver.LicenseImageUrl) &&
        !string.IsNullOrWhiteSpace(driver.VehicleImageUrl) &&
        driver.NationalIdExpiryDate.HasValue &&
        driver.DriverLicenseExpiryDate.HasValue &&
        driver.VehicleLicenseExpiryDate.HasValue &&
        !string.IsNullOrWhiteSpace(driver.Region) &&
        !string.IsNullOrWhiteSpace(driver.City) &&
        !DriverProfileReadinessFactory.HasExpiredRequiredDocuments(driver);

    private static void ResetDriverDocumentReviewIfReady(
        Domain.Modules.Delivery.Entities.Driver driver,
        DriverDocumentType type)
    {
        var hasPacket = type switch
        {
            DriverDocumentType.NationalId => DriverProfileReadinessFactory.HasNationalIdPacket(driver),
            DriverDocumentType.DriverLicense => DriverProfileReadinessFactory.HasDriverLicensePacket(driver),
            DriverDocumentType.VehicleLicense => DriverProfileReadinessFactory.HasVehicleLicensePacket(driver),
            _ => false
        };

        if (hasPacket)
        {
            driver.ResetDocumentReviewToPending(type);
        }
    }

    private void ApproveDriverDocumentReviewIfReady(
        Domain.Modules.Delivery.Entities.Driver driver,
        DriverDocumentType type,
        Guid reviewerUserId,
        string reviewerName)
    {
        var hasPacket = type switch
        {
            DriverDocumentType.NationalId => DriverProfileReadinessFactory.HasNationalIdPacket(driver),
            DriverDocumentType.DriverLicense => DriverProfileReadinessFactory.HasDriverLicensePacket(driver),
            DriverDocumentType.VehicleLicense => DriverProfileReadinessFactory.HasVehicleLicensePacket(driver),
            _ => false
        };

        if (!hasPacket)
        {
            return;
        }

        var existingReview = driver.DocumentReviews.FirstOrDefault(item => item.Type == type);
        var documentReview = existingReview ?? driver.GetOrCreateDocumentReview(type);
        if (existingReview is null)
        {
            dbContext.DriverDocumentReviews.Add(documentReview);
        }

        documentReview.Approve(reviewerUserId, reviewerName);
    }

    private async Task<string> ResolveDriverComplianceReviewerNameAsync(
        Guid reviewerUserId,
        CancellationToken cancellationToken)
    {
        var actor = await identityAccountService.FindByIdAsync(reviewerUserId, cancellationToken);
        return string.IsNullOrWhiteSpace(actor?.FullName) ? "Driver Compliance Desk" : actor.FullName;
    }

    private async Task NotifyDriverDocumentsApprovedAsync(
        Guid driverId,
        IReadOnlyList<DriverDocumentType> approvedDocumentTypes,
        CancellationToken cancellationToken)
    {
        if (approvedDocumentTypes.Count == 0)
        {
            return;
        }

        var driver = await dbContext.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == driverId, cancellationToken);

        if (driver is null)
        {
            return;
        }

        foreach (var documentType in approvedDocumentTypes)
        {
            await NotifyDriverDocumentApprovedAsync(driver, documentType, cancellationToken);
        }

        try
        {
            await notificationService.SendDriverHomeUpdatedAsync(driver.UserId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Driver home refresh notification failed after document profile approval for driver {DriverId}", driver.Id);
        }
    }

    private async Task NotifyDriverDocumentApprovedAsync(
        Driver driver,
        DriverDocumentType documentType,
        CancellationToken cancellationToken)
    {
        var documentNameAr = GetDriverDocumentNameAr(documentType);
        var documentNameEn = GetDriverDocumentNameEn(documentType);
        var titleAr = $"اعتمدنا {documentNameAr}";
        var titleEn = $"{documentNameEn} approved";
        var bodyAr = $"راجعنا {documentNameAr} واعتمدناه بعد تحديث المستند. تقدر تتابع حالة حسابك من التطبيق.";
        var bodyEn = $"Your updated {documentNameEn.ToLowerInvariant()} was reviewed and approved. You can track your account status in the app.";
        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.document_approved",
            driverId: driver.Id,
            titleAr: titleAr,
            titleEn: titleEn,
            bodyAr: bodyAr,
            bodyEn: bodyEn,
            extra: new
            {
                documentType = documentType.ToString(),
                documentId = documentType.ToString(),
                source = "profile_change_approval",
                verificationStatus = driver.VerificationStatus.ToString(),
                accountStatus = driver.Status.ToString()
            });

        try
        {
            await notificationService.SendToUserAsync(
                driver.UserId,
                new NotificationDispatchRequest(
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    NotificationTypes.DriverAccountUpdated,
                    NotificationCategories.Account,
                    NotificationPriorities.High,
                    driver.Id,
                    data),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Driver document profile approval inbox notification failed for driver {DriverId}", driver.Id);
        }

        try
        {
            await oneSignalPushService.SendMobileNotificationAsync(
                OneSignalMobilePushRequest.CreateHeadsUp(
                    driver.UserId.ToString(),
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    NotificationTypes.DriverAccountUpdated,
                    driver.Id,
                    data,
                    targetUrl: "/account-status",
                    category: NotificationCategories.Account,
                    targetApplication: OneSignalApplicationTarget.Driver),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Driver document profile approval push notification failed for driver {DriverId}", driver.Id);
        }
    }

    private async Task NotifyDriverDocumentsRejectedAsync(
        DriverDocumentsProfileChangePayload payload,
        string reason,
        CancellationToken cancellationToken)
    {
        var driver = await dbContext.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == payload.DriverId, cancellationToken);

        if (driver is null)
        {
            return;
        }

        var rejectedDocumentTypes = ResolveChangedDriverDocumentTypes(driver, payload);
        if (rejectedDocumentTypes.Count == 0)
        {
            return;
        }

        foreach (var documentType in rejectedDocumentTypes)
        {
            await NotifyDriverDocumentRejectedAsync(driver, documentType, reason, cancellationToken);
        }

        try
        {
            await notificationService.SendDriverHomeUpdatedAsync(driver.UserId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Driver home refresh notification failed after document profile rejection for driver {DriverId}", driver.Id);
        }
    }

    private async Task NotifyDriverDocumentRejectedAsync(
        Driver driver,
        DriverDocumentType documentType,
        string reason,
        CancellationToken cancellationToken)
    {
        var documentNameAr = GetDriverDocumentNameAr(documentType);
        var documentNameEn = GetDriverDocumentNameEn(documentType);
        var titleAr = $"رفضنا تحديث {documentNameAr}";
        var titleEn = $"{documentNameEn} update rejected";
        var bodyAr = $"رفضنا طلب تحديث {documentNameAr}. السبب: {reason}";
        var bodyEn = $"Your {documentNameEn.ToLowerInvariant()} update request was rejected. Reason: {reason}";
        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.document_rejected",
            driverId: driver.Id,
            titleAr: titleAr,
            titleEn: titleEn,
            bodyAr: bodyAr,
            bodyEn: bodyEn,
            extra: new
            {
                documentType = documentType.ToString(),
                documentId = documentType.ToString(),
                source = "profile_change_rejection",
                verificationStatus = driver.VerificationStatus.ToString(),
                accountStatus = driver.Status.ToString(),
                reason
            });

        try
        {
            await notificationService.SendToUserAsync(
                driver.UserId,
                new NotificationDispatchRequest(
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    NotificationTypes.DriverAccountUpdated,
                    NotificationCategories.Account,
                    NotificationPriorities.Critical,
                    driver.Id,
                    data),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Driver document profile rejection inbox notification failed for driver {DriverId}", driver.Id);
        }

        try
        {
            await oneSignalPushService.SendMobileNotificationAsync(
                OneSignalMobilePushRequest.CreateHeadsUp(
                    driver.UserId.ToString(),
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    NotificationTypes.DriverAccountUpdated,
                    driver.Id,
                    data,
                    targetUrl: "/account-status",
                    category: NotificationCategories.Account,
                    targetApplication: OneSignalApplicationTarget.Driver),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Driver document profile rejection push notification failed for driver {DriverId}", driver.Id);
        }
    }

    private async Task NotifyDriverProfileSectionApprovedAsync(
        Guid driverId,
        string section,
        CancellationToken cancellationToken)
    {
        var driver = await dbContext.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == driverId, cancellationToken);

        if (driver is null)
        {
            return;
        }

        var sectionNameAr = GetDriverProfileSectionNameAr(section);
        var sectionNameEn = GetDriverProfileSectionNameEn(section);
        var titleAr = $"اعتمدنا {sectionNameAr}";
        var titleEn = $"{sectionNameEn} approved";
        var bodyAr = $"راجعنا {sectionNameAr} واعتمدنا التعديل. تقدر تتابع حالة حسابك من التطبيق.";
        var bodyEn = $"Your {sectionNameEn.ToLowerInvariant()} update was reviewed and approved. You can track your account status in the app.";
        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.profile_section_approved",
            driverId: driver.Id,
            titleAr: titleAr,
            titleEn: titleEn,
            bodyAr: bodyAr,
            bodyEn: bodyEn,
            extra: new
            {
                section,
                source = "profile_change_approval",
                verificationStatus = driver.VerificationStatus.ToString(),
                accountStatus = driver.Status.ToString()
            });

        try
        {
            await notificationService.SendToUserAsync(
                driver.UserId,
                new NotificationDispatchRequest(
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    NotificationTypes.DriverAccountUpdated,
                    NotificationCategories.Account,
                    NotificationPriorities.High,
                    driver.Id,
                    data),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Driver profile section approval inbox notification failed for driver {DriverId}", driver.Id);
        }

        try
        {
            await oneSignalPushService.SendMobileNotificationAsync(
                OneSignalMobilePushRequest.CreateHeadsUp(
                    driver.UserId.ToString(),
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    NotificationTypes.DriverAccountUpdated,
                    driver.Id,
                    data,
                    targetUrl: "/account-status",
                    category: NotificationCategories.Account,
                    targetApplication: OneSignalApplicationTarget.Driver),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Driver profile section approval push notification failed for driver {DriverId}", driver.Id);
        }

        try
        {
            await notificationService.SendDriverHomeUpdatedAsync(driver.UserId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Driver home refresh notification failed after profile section approval for driver {DriverId}", driver.Id);
        }
    }

    private async Task NotifyDriverProfileSectionRejectedAsync(
        Guid driverId,
        string section,
        string reason,
        CancellationToken cancellationToken)
    {
        var driver = await dbContext.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == driverId, cancellationToken);

        if (driver is null)
        {
            return;
        }

        var sectionNameAr = GetDriverProfileSectionNameAr(section);
        var sectionNameEn = GetDriverProfileSectionNameEn(section);
        var titleAr = $"رفضنا تحديث {sectionNameAr}";
        var titleEn = $"{sectionNameEn} update rejected";
        var bodyAr = $"رفضنا طلب تحديث {sectionNameAr}. السبب: {reason}";
        var bodyEn = $"Your {sectionNameEn.ToLowerInvariant()} update request was rejected. Reason: {reason}";
        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.profile_section_rejected",
            driverId: driver.Id,
            titleAr: titleAr,
            titleEn: titleEn,
            bodyAr: bodyAr,
            bodyEn: bodyEn,
            extra: new
            {
                section,
                source = "profile_change_rejection",
                verificationStatus = driver.VerificationStatus.ToString(),
                accountStatus = driver.Status.ToString(),
                reason
            });

        try
        {
            await notificationService.SendToUserAsync(
                driver.UserId,
                new NotificationDispatchRequest(
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    NotificationTypes.DriverAccountUpdated,
                    NotificationCategories.Account,
                    NotificationPriorities.Critical,
                    driver.Id,
                    data),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Driver profile section rejection inbox notification failed for driver {DriverId}", driver.Id);
        }

        try
        {
            await oneSignalPushService.SendMobileNotificationAsync(
                OneSignalMobilePushRequest.CreateHeadsUp(
                    driver.UserId.ToString(),
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    NotificationTypes.DriverAccountUpdated,
                    driver.Id,
                    data,
                    targetUrl: "/account-status",
                    category: NotificationCategories.Account,
                    targetApplication: OneSignalApplicationTarget.Driver),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Driver profile section rejection push notification failed for driver {DriverId}", driver.Id);
        }

        try
        {
            await notificationService.SendDriverHomeUpdatedAsync(driver.UserId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Driver home refresh notification failed after profile section rejection for driver {DriverId}", driver.Id);
        }
    }

    private static string GetDriverProfileSectionNameAr(string section) =>
        section switch
        {
            "personal" => "البيانات الشخصية",
            "vehicle" => "بيانات المركبة",
            "documents" => "المستندات",
            _ => "قسم الملف الشخصي"
        };

    private static string GetDriverProfileSectionNameEn(string section) =>
        section switch
        {
            "personal" => "Personal information",
            "vehicle" => "Vehicle information",
            "documents" => "Documents",
            _ => "Profile section"
        };

    private static IReadOnlyList<DriverDocumentType> ResolveChangedDriverDocumentTypes(
        Driver driver,
        DriverDocumentsProfileChangePayload payload)
    {
        var changed = new List<DriverDocumentType>();

        if (HasChanged(driver.NationalIdFrontImageUrl, payload.NationalIdFrontImageUrl) ||
            HasChanged(driver.NationalIdBackImageUrl, payload.NationalIdBackImageUrl))
        {
            changed.Add(DriverDocumentType.NationalId);
        }

        if (HasChanged(driver.LicenseImageUrl, payload.LicenseImageUrl))
        {
            changed.Add(DriverDocumentType.DriverLicense);
        }

        if (HasChanged(driver.VehicleImageUrl, payload.VehicleImageUrl))
        {
            changed.Add(DriverDocumentType.VehicleLicense);
        }

        return changed;
    }

    private static string GetDriverDocumentNameAr(DriverDocumentType type) =>
        type switch
        {
            DriverDocumentType.NationalId => "الهوية الوطنية",
            DriverDocumentType.DriverLicense => "رخصة القيادة",
            DriverDocumentType.VehicleLicense => "رخصة المركبة",
            _ => "المستند"
        };

    private static string GetDriverDocumentNameEn(DriverDocumentType type) =>
        type switch
        {
            DriverDocumentType.NationalId => "National ID",
            DriverDocumentType.DriverLicense => "Driver license",
            DriverDocumentType.VehicleLicense => "Vehicle license",
            _ => "Document"
        };

    private static DriverPayoutMethodType ParseDriverPayoutMethodType(string value)
    {
        if (!Enum.TryParse<DriverPayoutMethodType>(value, true, out var methodType))
        {
            throw new BusinessRuleException("INVALID_DRIVER_PAYOUT_METHOD_TYPE", "Unsupported payout method type.");
        }

        return methodType;
    }

    private static void EnsureSupportedDriverBankPayoutMethod(
        DriverPayoutMethodType methodType,
        string accountIdentifier)
    {
        if (methodType != DriverPayoutMethodType.BankAccount)
        {
            throw new BusinessRuleException(
                "DRIVER_BANK_ACCOUNT_REQUIRED",
                "Only bank account payout methods are supported for withdrawals.");
        }

        if (!IsValidSaudiIban(accountIdentifier))
        {
            throw new BusinessRuleException(
                "DRIVER_BANK_IBAN_INVALID",
                "Driver bank account must be a valid Saudi IBAN.");
        }
    }

    private static bool IsValidSaudiIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return false;
        }

        var clean = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return clean.Length == 24 &&
            clean.StartsWith("SA", StringComparison.OrdinalIgnoreCase) &&
            clean.Skip(2).All(char.IsDigit);
    }

    private async Task<AccessApprovalRequestDto?> ProjectApprovalRequestAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var approval = await ProjectApprovalRequests(dbContext.AccessApprovalRequests.AsNoTracking().Where(item => item.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

        return approval is null
            ? null
            : approval with { ReviewDetails = await BuildReviewDetailsAsync(approval, cancellationToken) };
    }

    private IQueryable<AccessApprovalRequestDto> ProjectApprovalRequests(IQueryable<AccessApprovalRequest> query) =>
        from approval in query
        join requestedBy in dbContext.Users.AsNoTracking()
            on approval.RequestedByUserId equals requestedBy.Id
        join target in dbContext.Users.AsNoTracking()
            on approval.TargetUserId equals (Guid?)target.Id into targetJoin
        from target in targetJoin.DefaultIfEmpty()
        join decidedBy in dbContext.Users.AsNoTracking()
            on approval.DecidedByUserId equals (Guid?)decidedBy.Id into decidedByJoin
        from decidedBy in decidedByJoin.DefaultIfEmpty()
        select new AccessApprovalRequestDto(
            approval.Id,
            approval.RequestedByUserId,
            requestedBy.FullName,
            requestedBy.Email,
            approval.TargetUserId,
            target != null ? target.FullName : null,
            target != null ? target.Email : null,
            approval.Action,
            approval.Summary,
            approval.PayloadHash,
            approval.PayloadJson,
            approval.Status.ToString(),
            SaudiTime.ToSaudi(approval.CreatedAtUtc).ToString("O"),
            approval.DecidedByUserId,
            decidedBy != null ? decidedBy.FullName : null,
            decidedBy != null ? decidedBy.Email : null,
            approval.DecidedAtUtc == null ? null : approval.DecidedAtUtc.GetValueOrDefault().ToString("o"),
            approval.DecisionNote,
            approval.ConsumedAtUtc == null ? null : approval.ConsumedAtUtc.GetValueOrDefault().ToString("o"),
            null);

    private async Task<IReadOnlyList<AccessApprovalRequestDto>> AttachReviewDetailsAsync(
        IReadOnlyList<AccessApprovalRequestDto> approvals,
        CancellationToken cancellationToken)
    {
        var result = new List<AccessApprovalRequestDto>(approvals.Count);
        foreach (var approval in approvals)
        {
            result.Add(approval with
            {
                ReviewDetails = await BuildReviewDetailsAsync(approval, cancellationToken)
            });
        }

        return result;
    }

    private async Task<AccessApprovalReviewDetailsDto?> BuildReviewDetailsAsync(
        AccessApprovalRequestDto approval,
        CancellationToken cancellationToken)
    {
        return approval.Action switch
        {
            ProfileChangeApprovalActions.DriverProfilePersonal =>
                await BuildDriverPersonalReviewDetailsAsync(approval, cancellationToken),
            ProfileChangeApprovalActions.DriverProfileVehicle =>
                await BuildDriverVehicleReviewDetailsAsync(approval, cancellationToken),
            ProfileChangeApprovalActions.DriverProfileDocuments =>
                await BuildDriverDocumentsReviewDetailsAsync(approval, cancellationToken),
            ProfileChangeApprovalActions.DriverPayoutMethodCreate =>
                await BuildDriverPayoutMethodCreateReviewDetailsAsync(approval, cancellationToken),
            ProfileChangeApprovalActions.DriverPayoutMethodUpdate =>
                await BuildDriverPayoutMethodUpdateReviewDetailsAsync(approval, cancellationToken),
            ProfileChangeApprovalActions.DriverPayoutMethodMakePrimary =>
                await BuildDriverPayoutMethodMakePrimaryReviewDetailsAsync(approval, cancellationToken),
            ProfileChangeApprovalActions.DriverPayoutMethodDelete =>
                await BuildDriverPayoutMethodDeleteReviewDetailsAsync(approval, cancellationToken),
            _ => null
        };
    }

    private async Task<AccessApprovalReviewDetailsDto> BuildDriverPersonalReviewDetailsAsync(
        AccessApprovalRequestDto approval,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<DriverPersonalProfileChangePayload>(approval.PayloadJson, approval.Action);
        var driver = await dbContext.Drivers
            .AsNoTracking()
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == payload.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", payload.DriverId);

        return new AccessApprovalReviewDetailsDto(
            "driver",
            payload.DriverId,
            approval.Action,
            "update_personal",
            [
                ReviewField("fullName", "اسم المندوب", "Driver name", driver.User.FullName, payload.FullName, isSensitive: true),
                ReviewField("email", "البريد الإلكتروني", "Email", driver.User.Email, payload.Email, isSensitive: true),
                ReviewField("phone", "رقم الجوال", "Phone", driver.User.PhoneNumber, payload.Phone, isSensitive: true),
                ReviewField("address", "العنوان", "Address", driver.Address, payload.Address, isSensitive: true)
            ]);
    }

    private async Task<AccessApprovalReviewDetailsDto> BuildDriverVehicleReviewDetailsAsync(
        AccessApprovalRequestDto approval,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<DriverVehicleProfileChangePayload>(approval.PayloadJson, approval.Action);
        var driver = await dbContext.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == payload.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", payload.DriverId);

        return new AccessApprovalReviewDetailsDto(
            "driver",
            payload.DriverId,
            approval.Action,
            "update_vehicle",
            [
                ReviewField("vehicleType", "نوع المركبة", "Vehicle type", driver.VehicleType?.ToString(), payload.VehicleType, isSensitive: true),
                ReviewField("nationalId", "رقم الهوية", "National ID", driver.NationalId, payload.NationalId, isSensitive: true),
                ReviewField("licenseNumber", "رقم رخصة القيادة", "Driver license number", driver.LicenseNumber, payload.LicenseNumber, isSensitive: true),
                ReviewField("nationalIdExpiryDate", "تاريخ انتهاء الهوية", "National ID expiry date", FormatDate(driver.NationalIdExpiryDate), FormatDate(payload.NationalIdExpiryDate), isSensitive: true),
                ReviewField("driverLicenseExpiryDate", "تاريخ انتهاء رخصة القيادة", "Driver license expiry date", FormatDate(driver.DriverLicenseExpiryDate), FormatDate(payload.DriverLicenseExpiryDate), isSensitive: true),
                ReviewField("vehicleLicenseNumber", "رقم رخصة المركبة", "Vehicle license number", driver.VehicleLicenseNumber, payload.VehicleLicenseNumber, isSensitive: true),
                ReviewField("vehicleLicenseExpiryDate", "تاريخ انتهاء رخصة المركبة", "Vehicle license expiry date", FormatDate(driver.VehicleLicenseExpiryDate), FormatDate(payload.VehicleLicenseExpiryDate), isSensitive: true),
                ReviewField("region", "المنطقة", "Region", driver.Region, payload.Region),
                ReviewField("city", "المدينة", "City", driver.City, payload.City)
            ]);
    }

    private async Task<AccessApprovalReviewDetailsDto> BuildDriverDocumentsReviewDetailsAsync(
        AccessApprovalRequestDto approval,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<DriverDocumentsProfileChangePayload>(approval.PayloadJson, approval.Action);
        var driver = await dbContext.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == payload.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", payload.DriverId);

        return new AccessApprovalReviewDetailsDto(
            "driver",
            payload.DriverId,
            approval.Action,
            "update_documents",
            [
                DocumentReviewField("nationalIdFrontImageUrl", "صورة الهوية الأمامية", "National ID front image", driver.NationalIdFrontImageUrl, payload.NationalIdFrontImageUrl),
                DocumentReviewField("nationalIdBackImageUrl", "صورة الهوية الخلفية", "National ID back image", driver.NationalIdBackImageUrl, payload.NationalIdBackImageUrl),
                DocumentReviewField("licenseImageUrl", "صورة رخصة القيادة", "Driver license image", driver.LicenseImageUrl, payload.LicenseImageUrl),
                DocumentReviewField("vehicleImageUrl", "صورة المركبة", "Vehicle image", driver.VehicleImageUrl, payload.VehicleImageUrl),
                DocumentReviewField("personalPhotoUrl", "الصورة الشخصية", "Personal photo", driver.PersonalPhotoUrl, payload.PersonalPhotoUrl)
            ]);
    }

    private Task<AccessApprovalReviewDetailsDto> BuildDriverPayoutMethodCreateReviewDetailsAsync(
        AccessApprovalRequestDto approval,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<DriverPayoutMethodCreatePayload>(approval.PayloadJson, approval.Action);
        return Task.FromResult(new AccessApprovalReviewDetailsDto(
            "driver",
            payload.DriverId,
            approval.Action,
            "create_payout_method",
            [
                ReviewField("type", "نوع طريقة السحب", "Payout method type", null, payload.Type, isSensitive: true),
                ReviewField("accountHolderName", "اسم صاحب الحساب", "Account holder name", null, payload.AccountHolderName, isSensitive: true),
                ReviewField("accountIdentifier", "رقم الحساب / IBAN", "Account identifier / IBAN", null, payload.AccountIdentifier, isSensitive: true),
                ReviewField("providerName", "اسم البنك", "Provider name", null, payload.ProviderName, isSensitive: true),
                ReviewField("isPrimary", "أساسية", "Primary", null, payload.IsPrimary, isSensitive: true)
            ]));
    }

    private async Task<AccessApprovalReviewDetailsDto> BuildDriverPayoutMethodUpdateReviewDetailsAsync(
        AccessApprovalRequestDto approval,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<DriverPayoutMethodUpdatePayload>(approval.PayloadJson, approval.Action);
        var payoutMethod = await dbContext.DriverPayoutMethods
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == payload.PayoutMethodId && item.DriverId == payload.DriverId,
                cancellationToken)
            ?? throw new NotFoundException("DriverPayoutMethod", payload.PayoutMethodId);

        return new AccessApprovalReviewDetailsDto(
            "driver",
            payload.DriverId,
            approval.Action,
            "update_payout_method",
            [
                ReviewField("payoutMethodId", "معرف طريقة السحب", "Payout method ID", payoutMethod.Id, payload.PayoutMethodId, isSensitive: true),
                ReviewField("type", "نوع طريقة السحب", "Payout method type", payoutMethod.MethodType.ToString(), payload.Type, isSensitive: true),
                ReviewField("accountHolderName", "اسم صاحب الحساب", "Account holder name", payoutMethod.AccountHolderName, payload.AccountHolderName, isSensitive: true),
                ReviewField("accountIdentifier", "رقم الحساب / IBAN", "Account identifier / IBAN", payoutMethod.AccountIdentifier, payload.AccountIdentifier, isSensitive: true),
                ReviewField("providerName", "اسم البنك", "Provider name", payoutMethod.ProviderName, payload.ProviderName, isSensitive: true)
            ]);
    }

    private async Task<AccessApprovalReviewDetailsDto> BuildDriverPayoutMethodMakePrimaryReviewDetailsAsync(
        AccessApprovalRequestDto approval,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<DriverPayoutMethodMakePrimaryPayload>(approval.PayloadJson, approval.Action);
        var payoutMethod = await dbContext.DriverPayoutMethods
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == payload.PayoutMethodId && item.DriverId == payload.DriverId,
                cancellationToken)
            ?? throw new NotFoundException("DriverPayoutMethod", payload.PayoutMethodId);

        return new AccessApprovalReviewDetailsDto(
            "driver",
            payload.DriverId,
            approval.Action,
            "make_payout_method_primary",
            [
                ReviewField("payoutMethodId", "معرف طريقة السحب", "Payout method ID", payoutMethod.Id, payload.PayoutMethodId, isSensitive: true),
                ReviewField("maskedLabel", "طريقة السحب", "Payout method", payoutMethod.MaskedLabel, payoutMethod.MaskedLabel, isSensitive: true),
                ReviewField("isPrimary", "أساسية", "Primary", payoutMethod.IsPrimary, true, isSensitive: true)
            ]);
    }

    private async Task<AccessApprovalReviewDetailsDto> BuildDriverPayoutMethodDeleteReviewDetailsAsync(
        AccessApprovalRequestDto approval,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<DriverPayoutMethodDeletePayload>(approval.PayloadJson, approval.Action);
        var payoutMethod = await dbContext.DriverPayoutMethods
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == payload.PayoutMethodId && item.DriverId == payload.DriverId,
                cancellationToken)
            ?? throw new NotFoundException("DriverPayoutMethod", payload.PayoutMethodId);

        return new AccessApprovalReviewDetailsDto(
            "driver",
            payload.DriverId,
            approval.Action,
            "delete_payout_method",
            [
                ReviewField("payoutMethodId", "معرف طريقة السحب", "Payout method ID", payoutMethod.Id, null, isSensitive: true),
                ReviewField("type", "نوع طريقة السحب", "Payout method type", payoutMethod.MethodType.ToString(), null, isSensitive: true),
                ReviewField("accountHolderName", "اسم صاحب الحساب", "Account holder name", payoutMethod.AccountHolderName, null, isSensitive: true),
                ReviewField("accountIdentifier", "رقم الحساب / IBAN", "Account identifier / IBAN", payoutMethod.AccountIdentifier, null, isSensitive: true),
                ReviewField("providerName", "اسم البنك", "Provider name", payoutMethod.ProviderName, null, isSensitive: true),
                ReviewField("isPrimary", "أساسية", "Primary", payoutMethod.IsPrimary, null, isSensitive: true)
            ]);
    }

    private static AccessApprovalReviewFieldDto ReviewField(
        string key,
        string labelAr,
        string labelEn,
        object? currentValue,
        object? requestedValue,
        bool isDocument = false,
        bool isSensitive = false) =>
        new(
            key,
            labelAr,
            labelEn,
            currentValue,
            requestedValue,
            !ValuesEqual(currentValue, requestedValue),
            isDocument,
            isSensitive);

    private static AccessApprovalReviewFieldDto DocumentReviewField(
        string key,
        string labelAr,
        string labelEn,
        string? currentValue,
        string? requestedValue) =>
        new(
            key,
            labelAr,
            labelEn,
            currentValue,
            requestedValue,
            HasChanged(currentValue, requestedValue),
            IsDocument: true,
            IsSensitive: true);

    private static string? FormatDate(DateTime? value) =>
        value?.Date.ToString("yyyy-MM-dd");

    private static bool ValuesEqual(object? currentValue, object? requestedValue) =>
        string.Equals(
            Convert.ToString(currentValue, System.Globalization.CultureInfo.InvariantCulture)?.Trim(),
            Convert.ToString(requestedValue, System.Globalization.CultureInfo.InvariantCulture)?.Trim(),
            StringComparison.OrdinalIgnoreCase);

    private static TPayload DeserializePayload<TPayload>(string payloadJson, string action)
    {
        return JsonSerializer.Deserialize<TPayload>(payloadJson, JsonOptions)
            ?? throw new BusinessRuleException(
                "INVALID_APPROVAL_PAYLOAD",
                $"Approval payload for {action} is invalid.");
    }

    private static object SnapshotApproval(AccessApprovalRequest approval) => new
    {
        approval.Id,
        approval.RequestedByUserId,
        approval.TargetUserId,
        approval.Action,
        approval.Summary,
        approval.PayloadHash,
        Status = approval.Status.ToString(),
        approval.CreatedAtUtc,
        approval.DecidedByUserId,
        approval.DecidedAtUtc,
        approval.DecisionNote,
        approval.ConsumedAtUtc
    };
}

// Request DTOs
public record UpdateUserScopeRequest(
    Guid RoleDefinitionId,
    PanelScope PanelScope,
    AccessScopeType ScopeType,
    Guid? ScopeEntityId,
    string? Notes
);

public record UpdateUserOverridesRequest(
    List<string> GrantedPermissions,
    List<string> RevokedPermissions
);

public record CreateAdminAccessUserRequest(
    string FullName,
    string Email,
    string Phone,
    string Password,
    Guid RoleDefinitionId,
    PanelScope PanelScope,
    AccessScopeType ScopeType,
    Guid? ScopeEntityId,
    string? Department,
    string? Team,
    string? Notes
);

public record UpdateAdminAccessUserRequest(
    string FullName,
    string Email,
    string Phone,
    Guid RoleDefinitionId,
    PanelScope PanelScope,
    AccessScopeType ScopeType,
    Guid? ScopeEntityId,
    string? Department,
    string? Team,
    string? Status,
    string? Notes,
    List<string> GrantedPermissions,
    List<string> RevokedPermissions,
    DirectoryCommunicationProfileDto? Communication
);

public record ResetTemporaryPasswordRequest(string TemporaryPassword);

public record AccessApprovalDecisionRequest(string? Note);
