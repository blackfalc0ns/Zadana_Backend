using Microsoft.AspNetCore.Mvc;
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
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
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
    IAccessAuditService auditService) : ApiControllerBase
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
                    log.CreatedAtUtc.ToString("o"),
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

        return Ok(result);
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
        if (ProfileChangeApprovalActions.IsProfileChange(approval.Action))
        {
            await ApplyApprovedProfileChangeAsync(approval, cancellationToken);
            approval.Consume();
        }
        auditService.Add(
            approval.TargetUserId ?? approval.RequestedByUserId,
            "access-approval-approved",
            $"Access approval request {approval.Action} was approved.",
            before,
            SnapshotApproval(approval));

        await dbContext.SaveChangesAsync(cancellationToken);
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
                    log.CreatedAtUtc.ToString("o"),
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

    private async Task ApplyApprovedProfileChangeAsync(
        AccessApprovalRequest approval,
        CancellationToken cancellationToken)
    {
        switch (approval.Action)
        {
            case ProfileChangeApprovalActions.VendorProfileBasic:
                await ApplyVendorBasicChangeAsync(
                    DeserializePayload<VendorBasicProfileChangePayload>(approval),
                    cancellationToken);
                break;

            case ProfileChangeApprovalActions.VendorProfileStore:
                await ApplyVendorStoreChangeAsync(
                    DeserializePayload<VendorStoreProfileChangePayload>(approval),
                    cancellationToken);
                break;

            case ProfileChangeApprovalActions.VendorProfileOwner:
                await ApplyVendorOwnerChangeAsync(
                    DeserializePayload<VendorOwnerProfileChangePayload>(approval),
                    cancellationToken);
                break;

            case ProfileChangeApprovalActions.VendorProfileLegal:
                await ApplyVendorLegalChangeAsync(
                    DeserializePayload<VendorLegalProfileChangePayload>(approval),
                    cancellationToken);
                break;

            case ProfileChangeApprovalActions.VendorProfileBanking:
                await ApplyVendorBankingChangeAsync(
                    DeserializePayload<VendorBankingProfileChangePayload>(approval),
                    cancellationToken);
                break;

            case ProfileChangeApprovalActions.DriverProfilePersonal:
                await ApplyDriverPersonalChangeAsync(
                    DeserializePayload<DriverPersonalProfileChangePayload>(approval),
                    cancellationToken);
                break;

            case ProfileChangeApprovalActions.DriverProfileVehicle:
                await ApplyDriverVehicleChangeAsync(
                    DeserializePayload<DriverVehicleProfileChangePayload>(approval),
                    cancellationToken);
                break;

            case ProfileChangeApprovalActions.DriverProfileDocuments:
                await ApplyDriverDocumentsChangeAsync(
                    DeserializePayload<DriverDocumentsProfileChangePayload>(approval),
                    cancellationToken);
                break;

            case ProfileChangeApprovalActions.DriverPayoutMethodCreate:
                await ApplyDriverPayoutMethodCreateAsync(
                    DeserializePayload<DriverPayoutMethodCreatePayload>(approval),
                    cancellationToken);
                break;

            case ProfileChangeApprovalActions.DriverPayoutMethodUpdate:
                await ApplyDriverPayoutMethodUpdateAsync(
                    DeserializePayload<DriverPayoutMethodUpdatePayload>(approval),
                    cancellationToken);
                break;

            case ProfileChangeApprovalActions.DriverPayoutMethodMakePrimary:
                await ApplyDriverPayoutMethodMakePrimaryAsync(
                    DeserializePayload<DriverPayoutMethodMakePrimaryPayload>(approval),
                    cancellationToken);
                break;

            case ProfileChangeApprovalActions.DriverPayoutMethodDelete:
                await ApplyDriverPayoutMethodDeleteAsync(
                    DeserializePayload<DriverPayoutMethodDeletePayload>(approval),
                    cancellationToken);
                break;

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
        driver.RefreshProfileReviewState(HasRequiredDriverProfileData(driver), sensitiveChange: true, note: "Sensitive profile change approved by admin.");
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

        DriverVehicleType? parsedVehicleType = null;
        if (!string.IsNullOrWhiteSpace(payload.VehicleType))
        {
            if (!DriverVehicleTypeMapper.TryParse(payload.VehicleType, out var resolvedVehicleType))
            {
                throw new BusinessRuleException("INVALID_VEHICLE_TYPE", "Unsupported vehicle type.");
            }

            parsedVehicleType = resolvedVehicleType;
        }

        driver.UpdateDetails(
            parsedVehicleType,
            payload.NationalId,
            payload.LicenseNumber,
            payload.NationalIdExpiryDate,
            payload.DriverLicenseExpiryDate,
            payload.VehicleLicenseNumber,
            payload.VehicleLicenseExpiryDate);

        ResetDriverDocumentReviewIfReady(driver, DriverDocumentType.NationalId);
        ResetDriverDocumentReviewIfReady(driver, DriverDocumentType.DriverLicense);
        ResetDriverDocumentReviewIfReady(driver, DriverDocumentType.VehicleLicense);

        driver.UpdateServiceArea(payload.Region, payload.City);
        driver.RefreshProfileReviewState(HasRequiredDriverProfileData(driver), sensitiveChange: true, note: "Sensitive profile change approved by admin.");
    }

    private async Task ApplyDriverDocumentsChangeAsync(
        DriverDocumentsProfileChangePayload payload,
        CancellationToken cancellationToken)
    {
        var driver = await dbContext.Drivers
            .Include(item => item.User)
            .Include(item => item.DocumentReviews)
            .FirstOrDefaultAsync(item => item.Id == payload.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", payload.DriverId);

        driver.UpdateDocuments(
            payload.NationalIdFrontImageUrl,
            payload.NationalIdBackImageUrl,
            payload.LicenseImageUrl,
            payload.VehicleImageUrl,
            payload.PersonalPhotoUrl);

        ResetDriverDocumentReviewIfReady(driver, DriverDocumentType.NationalId);
        ResetDriverDocumentReviewIfReady(driver, DriverDocumentType.DriverLicense);
        ResetDriverDocumentReviewIfReady(driver, DriverDocumentType.VehicleLicense);

        driver.RefreshProfileReviewState(HasRequiredDriverProfileData(driver), sensitiveChange: true, note: "Documents approved for profile update by admin.");
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
        CancellationToken cancellationToken) =>
        await ProjectApprovalRequests(dbContext.AccessApprovalRequests.AsNoTracking().Where(item => item.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

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
            approval.CreatedAtUtc.ToString("o"),
            approval.DecidedByUserId,
            decidedBy != null ? decidedBy.FullName : null,
            decidedBy != null ? decidedBy.Email : null,
            approval.DecidedAtUtc == null ? null : approval.DecidedAtUtc.GetValueOrDefault().ToString("o"),
            approval.DecisionNote,
            approval.ConsumedAtUtc == null ? null : approval.ConsumedAtUtc.GetValueOrDefault().ToString("o"));

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
