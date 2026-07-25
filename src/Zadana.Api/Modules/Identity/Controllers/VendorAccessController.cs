using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Authorization;
using Zadana.Api.Controllers;
using Zadana.Api.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Geography.Support;
using Zadana.Application.Modules.Identity.Commands.UpdateUserOverrides;
using Zadana.Application.Modules.Identity.Commands.UpdateUserScope;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Identity.Queries.GetUserEffectiveAccess;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Identity.Constants;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/vendors")]
[Tags("Vendor App API")]
public class VendorAccessController : ApiControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    private readonly IApplicationDbContext _context;
    private readonly ICurrentVendorService _currentVendorService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VendorAccessController> _logger;

    public VendorAccessController(
        IApplicationDbContext context,
        ICurrentVendorService currentVendorService,
        ICurrentUserService currentUserService,
        IIdentityAccountService identityAccountService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<VendorAccessController> logger)
    {
        _context = context;
        _currentVendorService = currentVendorService;
        _currentUserService = currentUserService;
        _identityAccountService = identityAccountService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpGet("branches")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public async Task<IActionResult> GetBranches(CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var query = _context.VendorBranches
            .AsNoTracking()
            .Include(branch => branch.OperatingHours)
            .Where(branch => branch.VendorId == scope.VendorId);

        if (scope.BranchId.HasValue)
        {
            query = query.Where(branch => branch.Id == scope.BranchId.Value);
        }

        var branches = await query
            .OrderByDescending(branch => branch.IsPrimary)
            .ThenBy(branch => branch.CreatedAtUtc)
            .ThenBy(branch => branch.Name)
            .ToListAsync(cancellationToken);

        return Ok(branches.Select(ToBranchResponse));
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPost("branches")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamCreate)]
    public async Task<IActionResult> CreateBranch(
        [FromBody] VendorBranchCreateRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        if (scope.BranchId.HasValue)
        {
            return Forbid();
        }

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? throw new BusinessRuleException("BRANCH_NAME_REQUIRED", "Branch name is required.")
            : request.Name.Trim();
        var addressLine = string.IsNullOrWhiteSpace(request.AddressLine)
            ? throw new BusinessRuleException("BRANCH_ADDRESS_REQUIRED", "Branch address is required.")
            : request.AddressLine.Trim();
        var contactPhone = (request.Phone ?? string.Empty).Trim();
        if (contactPhone.Length > 20)
        {
            contactPhone = contactPhone[..20];
        }
        var deliveryRadiusKm = request.DeliveryRadiusKm.GetValueOrDefault(5m);
        if (deliveryRadiusKm <= 0)
        {
            deliveryRadiusKm = 5m;
        }

        var code = string.IsNullOrWhiteSpace(request.Code)
            ? GenerateBranchCode(name)
            : request.Code.Trim();
        var managerName = string.IsNullOrWhiteSpace(request.ManagerName)
            ? throw new BusinessRuleException("BRANCH_MANAGER_NAME_REQUIRED", "Branch manager name is required.")
            : request.ManagerName.Trim();
        var managerContact = string.IsNullOrWhiteSpace(request.ManagerContact)
            ? throw new BusinessRuleException("BRANCH_MANAGER_CONTACT_REQUIRED", "Branch manager contact is required.")
            : request.ManagerContact.Trim();
        var region = string.IsNullOrWhiteSpace(request.Region)
            ? throw new BusinessRuleException("BRANCH_REGION_REQUIRED", "لازم تختار منطقة الفرع.")
            : request.Region.Trim();
        var city = string.IsNullOrWhiteSpace(request.City)
            ? throw new BusinessRuleException("BRANCH_CITY_REQUIRED", "لازم تختار مدينة الفرع.")
            : request.City.Trim();
        await OperationalGeographyScope.EnsureOperationalRegionCityAsync(
            _context,
            region,
            city,
            cancellationToken);
        var isPrimary = request.IsPrimary || !await _context.VendorBranches
            .AsNoTracking()
            .AnyAsync(branch => branch.VendorId == scope.VendorId, cancellationToken);

        var duplicateCodeExists = await _context.VendorBranches
            .AsNoTracking()
            .AnyAsync(branch => branch.VendorId == scope.VendorId && branch.Code == code, cancellationToken);
        if (duplicateCodeExists)
        {
            throw new BusinessRuleException("BRANCH_CODE_CONFLICT", "Branch code is already in use.");
        }

        if (isPrimary)
        {
            var existingPrimary = await _context.VendorBranches
                .Where(branch => branch.VendorId == scope.VendorId && branch.IsPrimary)
                .ToListAsync(cancellationToken);

            foreach (var existingBranch in existingPrimary)
            {
                existingBranch.SetPrimary(false);
            }
        }

        var branch = new VendorBranch(
            scope.VendorId,
            name,
            code,
            isPrimary,
            addressLine,
            region,
            city,
            request.Latitude ?? 0,
            request.Longitude ?? 0,
            contactPhone,
            managerName,
            managerContact,
            deliveryRadiusKm);

        _context.VendorBranches.Add(branch);

        foreach (var operatingHour in NormalizeOperatingHours(branch.Id, request.OperatingHours))
        {
            _context.BranchOperatingHours.Add(operatingHour);
        }

        await _context.SaveChangesAsync(cancellationToken);

        branch = await _context.VendorBranches
            .AsNoTracking()
            .Include(item => item.OperatingHours)
            .FirstAsync(item => item.Id == branch.Id, cancellationToken);

        return Ok(ToBranchResponse(branch));
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPut("branches/{id:guid}")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public async Task<IActionResult> UpdateBranch(
        Guid id,
        [FromBody] VendorBranchUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await RequireCompanyScopeForMutationAsync(cancellationToken);
        var branch = await _context.VendorBranches
            .Include(item => item.OperatingHours)
            .FirstOrDefaultAsync(item => item.Id == id && item.VendorId == scope.VendorId, cancellationToken)
            ?? throw new NotFoundException("VendorBranch", id);

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? throw new BusinessRuleException("BRANCH_NAME_REQUIRED", "Branch name is required.")
            : request.Name.Trim();
        var addressLine = string.IsNullOrWhiteSpace(request.AddressLine)
            ? throw new BusinessRuleException("BRANCH_ADDRESS_REQUIRED", "Branch address is required.")
            : request.AddressLine.Trim();
        var contactPhone = (request.Phone ?? string.Empty).Trim();
        if (contactPhone.Length > 20)
        {
            contactPhone = contactPhone[..20];
        }

        var code = string.IsNullOrWhiteSpace(request.Code)
            ? GenerateBranchCode(name)
            : request.Code.Trim();
        var managerName = string.IsNullOrWhiteSpace(request.ManagerName)
            ? throw new BusinessRuleException("BRANCH_MANAGER_NAME_REQUIRED", "Branch manager name is required.")
            : request.ManagerName.Trim();
        var managerContact = string.IsNullOrWhiteSpace(request.ManagerContact)
            ? throw new BusinessRuleException("BRANCH_MANAGER_CONTACT_REQUIRED", "Branch manager contact is required.")
            : request.ManagerContact.Trim();
        var region = string.IsNullOrWhiteSpace(request.Region)
            ? throw new BusinessRuleException("BRANCH_REGION_REQUIRED", "لازم تختار منطقة الفرع.")
            : request.Region.Trim();
        var city = string.IsNullOrWhiteSpace(request.City)
            ? throw new BusinessRuleException("BRANCH_CITY_REQUIRED", "لازم تختار مدينة الفرع.")
            : request.City.Trim();
        await OperationalGeographyScope.EnsureOperationalRegionCityAsync(
            _context,
            region,
            city,
            cancellationToken);
        var latitude = request.Latitude ?? branch.Latitude;
        var longitude = request.Longitude ?? branch.Longitude;
        var deliveryRadiusKm = request.DeliveryRadiusKm.GetValueOrDefault(branch.DeliveryRadiusKm);
        if (latitude is < -90 or > 90)
        {
            throw new BusinessRuleException("BRANCH_LATITUDE_INVALID", "Latitude must be between -90 and 90.");
        }
        if (longitude is < -180 or > 180)
        {
            throw new BusinessRuleException("BRANCH_LONGITUDE_INVALID", "Longitude must be between -180 and 180.");
        }
        if (deliveryRadiusKm <= 0)
        {
            throw new BusinessRuleException("BRANCH_RADIUS_INVALID", "Delivery radius must be greater than zero.");
        }

        var duplicateCodeExists = await _context.VendorBranches
            .AsNoTracking()
            .AnyAsync(item => item.VendorId == scope.VendorId && item.Id != branch.Id && item.Code == code, cancellationToken);
        if (duplicateCodeExists)
        {
            throw new BusinessRuleException("BRANCH_CODE_CONFLICT", "Branch code is already in use.");
        }

        var isPrimary = request.IsPrimary || branch.IsPrimary;
        if (isPrimary)
        {
            var existingPrimary = await _context.VendorBranches
                .Where(item => item.VendorId == scope.VendorId && item.Id != branch.Id && item.IsPrimary)
                .ToListAsync(cancellationToken);

            foreach (var existingBranch in existingPrimary)
            {
                existingBranch.SetPrimary(false);
            }
        }

        branch.Update(
            name,
            code,
            isPrimary,
            addressLine,
            region,
            city,
            latitude,
            longitude,
            contactPhone,
            managerName,
            managerContact,
            deliveryRadiusKm);

        if (branch.OperatingHours.Count > 0)
        {
            _context.BranchOperatingHours.RemoveRange(branch.OperatingHours);
        }

        foreach (var operatingHour in NormalizeOperatingHours(branch.Id, request.OperatingHours))
        {
            _context.BranchOperatingHours.Add(operatingHour);
        }

        await _context.SaveChangesAsync(cancellationToken);

        branch = await _context.VendorBranches
            .AsNoTracking()
            .Include(item => item.OperatingHours)
            .FirstAsync(item => item.Id == id, cancellationToken);

        return Ok(ToBranchResponse(branch));
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpGet("staff")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public async Task<IActionResult> GetStaff(CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var staff = await LoadStaffAsync(scope, cancellationToken);
        return Ok(staff);
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpGet("staff/{id:guid}")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public async Task<IActionResult> GetStaffMember(Guid id, CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var member = await FindStaffMemberAsync(scope, id, cancellationToken);
        return Ok(member);
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpGet("staff/invitations")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public async Task<IActionResult> GetInvitations(CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        await ExpireDueInvitationsAsync(scope.VendorId, cancellationToken);

        var invitations = await _context.VendorStaffInvitations
            .AsNoTracking()
            .Where(invitation => invitation.VendorId == scope.VendorId)
            .OrderByDescending(invitation => invitation.SentAtUtc)
            .ToListAsync(cancellationToken);

        var filteredInvitations = scope.BranchId.HasValue
            ? invitations.Where(invitation => ParseBranchIds(invitation.BranchIdsJson).Contains(scope.BranchId.Value))
            : invitations;

        return Ok(filteredInvitations.Select(invitation => ToResponse(invitation, null)));
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpGet("staff/invitations/{id:guid}")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public async Task<IActionResult> GetInvitation(Guid id, CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        await ExpireDueInvitationsAsync(scope.VendorId, cancellationToken);

        var invitation = await _context.VendorStaffInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.VendorId == scope.VendorId, cancellationToken)
            ?? throw new NotFoundException("VendorStaffInvitation", id);

        EnsureInvitationInScope(invitation, scope);

        return Ok(ToResponse(invitation, null));
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPost("staff/invitations")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamCreate)]
    public async Task<IActionResult> CreateInvitation(
        [FromBody] VendorStaffInvitationCreateRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var currentUserId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await RequireVendorAsync(scope.VendorId, cancellationToken);
        var normalized = NormalizeCreateRequest(request);
        await EnsureInvitationBranchesAsync(scope.VendorId, normalized.BranchIds, cancellationToken);
        EnsureBranchSelectionAllowed(normalized.BranchIds, scope);
        await EnsureNotAlreadyStaffAsync(scope.VendorId, normalized.Email, cancellationToken);

        var now = DateTime.UtcNow;
        var token = GenerateToken();
        var tokenHash = HashToken(token);
        var expiresAt = now.Add(InvitationLifetime);
        var branchIdsJson = JsonSerializer.Serialize(normalized.BranchIds, JsonOptions);
        var permissionsJson = JsonSerializer.Serialize(normalized.Permissions, JsonOptions);

        var invitation = await _context.VendorStaffInvitations
            .FirstOrDefaultAsync(item =>
                item.VendorId == scope.VendorId &&
                item.Email == normalized.Email &&
                item.Status != VendorStaffInvitation.StatusAccepted &&
                item.Status != VendorStaffInvitation.StatusRevoked,
                cancellationToken);

        if (invitation is null)
        {
                invitation = new VendorStaffInvitation(
                scope.VendorId,
                currentUserId,
                normalized.Type,
                normalized.TargetName,
                normalized.Email,
                normalized.RoleTemplate,
                branchIdsJson,
                permissionsJson,
                tokenHash,
                now,
                expiresAt,
                normalized.InviteMessage);

            _context.VendorStaffInvitations.Add(invitation);
        }
        else
        {
            invitation.RefreshDetails(
                normalized.Type,
                normalized.TargetName,
                normalized.RoleTemplate,
                branchIdsJson,
                permissionsJson,
                normalized.InviteMessage,
                tokenHash,
                now,
                expiresAt);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var inviteLink = BuildInviteLink(token);
        var sendResult = await SendInvitationEmailAsync(vendor, invitation, inviteLink, cancellationToken);
        invitation.MarkSendResult(sendResult.Success, sendResult.ProviderMessageId, sendResult.FailureReason);
        await _context.SaveChangesAsync(cancellationToken);

        var response = ToResponse(invitation, inviteLink);
        return Ok(response);
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPost("staff/invitations/{id:guid}/resend")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public async Task<IActionResult> ResendInvitation(Guid id, CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var vendor = await RequireVendorAsync(scope.VendorId, cancellationToken);
        var invitation = await RequireInvitationAsync(scope.VendorId, id, cancellationToken);
        EnsureInvitationInScope(invitation, scope);

        if (invitation.Status == VendorStaffInvitation.StatusAccepted)
        {
            throw new BusinessRuleException("INVITATION_ALREADY_ACCEPTED", "This invitation has already been accepted.");
        }

        if (invitation.Status == VendorStaffInvitation.StatusRevoked)
        {
            throw new BusinessRuleException("INVITATION_REVOKED", "This invitation was revoked.");
        }

        var now = DateTime.UtcNow;
        var token = GenerateToken();
        invitation.RotateToken(HashToken(token), now, now.Add(InvitationLifetime));
        await _context.SaveChangesAsync(cancellationToken);

        var inviteLink = BuildInviteLink(token);
        var sendResult = await SendInvitationEmailAsync(vendor, invitation, inviteLink, cancellationToken);
        invitation.MarkSendResult(sendResult.Success, sendResult.ProviderMessageId, sendResult.FailureReason);
        await _context.SaveChangesAsync(cancellationToken);

        var response = ToResponse(invitation, inviteLink);
        return Ok(response);
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPost("staff/invitations/{id:guid}/revoke")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public async Task<IActionResult> RevokeInvitation(Guid id, CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var invitation = await RequireInvitationAsync(scope.VendorId, id, cancellationToken);
        EnsureInvitationInScope(invitation, scope);
        invitation.Revoke(DateTime.UtcNow);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(invitation, null));
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpDelete("staff/invitations/{id:guid}")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public async Task<IActionResult> DeleteInvitation(Guid id, CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var invitation = await RequireInvitationAsync(scope.VendorId, id, cancellationToken);
        EnsureInvitationInScope(invitation, scope);

        _context.VendorStaffInvitations.Remove(invitation);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpDelete("branches/{id:guid}")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public async Task<IActionResult> DeleteBranch(Guid id, CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        if (scope.BranchId.HasValue)
        {
            return Forbid();
        }

        var branch = await _context.VendorBranches
            .Include(item => item.OperatingHours)
            .FirstOrDefaultAsync(item => item.Id == id && item.VendorId == scope.VendorId, cancellationToken)
            ?? throw new NotFoundException("VendorBranch", id);

        if (branch.IsPrimary)
        {
            throw new BusinessRuleException(
                "PRIMARY_BRANCH_DELETE_FORBIDDEN",
                "The primary branch cannot be deleted.");
        }

        var hasProducts = await _context.VendorProducts
            .AsNoTracking()
            .AnyAsync(item => item.VendorBranchId == branch.Id, cancellationToken);
        if (hasProducts)
        {
            throw new BusinessRuleException(
                "BRANCH_DELETE_BLOCKED_PRODUCTS",
                "This branch cannot be deleted because products are assigned to it.");
        }

        await BranchActivePickupOrdersSupport.EnsureNoActivePickupOrdersAsync(_context, branch.Id, cancellationToken);

        var hasOrders = await _context.Orders
            .AsNoTracking()
            .AnyAsync(item => item.VendorBranchId == branch.Id, cancellationToken);
        if (hasOrders)
        {
            throw new BusinessRuleException(
                "BRANCH_DELETE_BLOCKED_ORDERS",
                "This branch cannot be deleted because orders are assigned to it.");
        }

        var hasStaffScopes = await _context.UserAccessScopes
            .Where(item =>
                item.IsActive &&
                item.PanelScope == PanelScope.VendorPanel &&
                item.ScopeType == AccessScopeType.VendorBranch &&
                item.ScopeEntityId == branch.Id)
            .ToListAsync(cancellationToken);

        foreach (var staffScope in hasStaffScopes)
        {
            staffScope.Deactivate();
        }

        var linkedInvitations = await _context.VendorStaffInvitations
            .Where(item => item.VendorId == scope.VendorId)
            .ToListAsync(cancellationToken);
        var invitationsToRemove = linkedInvitations
            .Where(item => ParseBranchIds(item.BranchIdsJson).Contains(branch.Id))
            .ToList();

        if (invitationsToRemove.Count > 0)
        {
            _context.VendorStaffInvitations.RemoveRange(invitationsToRemove);
        }

        if (branch.OperatingHours.Count > 0)
        {
            _context.BranchOperatingHours.RemoveRange(branch.OperatingHours);
        }

        _context.VendorBranches.Remove(branch);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("staff/invitations/accept/{token}")]
    public async Task<IActionResult> GetInvitationForAcceptance(string token, CancellationToken cancellationToken)
    {
        var invitation = await FindInvitationByTokenAsync(token, cancellationToken);
        var now = DateTime.UtcNow;
        if (!invitation.CanBeAccepted(now))
        {
            if (invitation.ExpiresAtUtc <= now)
            {
                invitation.Expire(now);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return BadRequest(new { code = "INVITATION_NOT_ACTIVE", message = ApiLocalizedMessages.Resolve(HttpContext, "VENDOR_INVITATION_UNAVAILABLE") });
        }

        var vendor = await RequireVendorAsync(invitation.VendorId, cancellationToken);
        return Ok(new VendorStaffInvitationAcceptancePreview(
            invitation.Id,
            invitation.Type,
            invitation.TargetName,
            invitation.Email,
            ResolveVendorName(vendor),
            ReadBranchIds(invitation.BranchIdsJson),
            invitation.ExpiresAtUtc));
    }

    [AllowAnonymous]
    [HttpPost("staff/invitations/accept")]
    public async Task<IActionResult> AcceptInvitation(
        [FromBody] VendorStaffInvitationAcceptRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return BadRequest(new { code = "WEAK_PASSWORD", message = ApiLocalizedMessages.Resolve(HttpContext, "PASSWORD_MIN_LENGTH") });
        }

        var invitation = await FindInvitationByTokenAsync(request.Token, cancellationToken);
        var now = DateTime.UtcNow;
        if (!invitation.CanBeAccepted(now))
        {
            if (invitation.ExpiresAtUtc <= now)
            {
                invitation.Expire(now);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return BadRequest(new { code = "INVITATION_NOT_ACTIVE", message = ApiLocalizedMessages.Resolve(HttpContext, "VENDOR_INVITATION_UNAVAILABLE") });
        }

        await EnsureNotAlreadyStaffAsync(invitation.VendorId, invitation.Email, cancellationToken);

        var roleCode = ResolveRoleCode(invitation.RoleTemplate);
        var role = await _context.RoleDefinitions
            .FirstOrDefaultAsync(item => item.Code == roleCode && item.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("ROLE_NOT_CONFIGURED", $"Role {roleCode} is not configured.");

        var fullName = string.IsNullOrWhiteSpace(request.FullName)
            ? invitation.TargetName
            : request.FullName.Trim();
        var existingAccount = await _identityAccountService.FindByIdentifierAsync(invitation.Email, cancellationToken);

        User user;
        if (existingAccount is not null)
        {
            if (existingAccount.Role != UserRole.VendorStaff)
            {
                return Conflict(new { code = "USER_ALREADY_EXISTS", message = ApiLocalizedMessages.Resolve(HttpContext, "STAFF_EMAIL_ALREADY_EXISTS") });
            }

            var linkedVendorIds = await GetLinkedVendorIdsAsync(existingAccount.Id, cancellationToken);
            if (linkedVendorIds.Any(vendorId => vendorId != invitation.VendorId))
            {
                return Conflict(new
                {
                    code = "STAFF_ACCOUNT_LINKED_TO_ANOTHER_VENDOR",
                    message = ApiLocalizedMessages.Resolve(HttpContext, "STAFF_LINKED_TO_ANOTHER_VENDOR")
                });
            }

            var resetResult = await _identityAccountService.ResetPasswordByAdminAsync(existingAccount.Id, request.Password, cancellationToken);
            if (!resetResult.Succeeded)
            {
                return BadRequest(new
                {
                    code = "IDENTITY_PASSWORD_RESET_FAILED",
                    message = ApiLocalizedMessages.Resolve(HttpContext, "IDENTITY_PASSWORD_RESET_FAILED")
                });
            }

            await _identityAccountService.ActivateAsync(existingAccount.Id, cancellationToken);
            await _identityAccountService.UnlockLoginAsync(existingAccount.Id, cancellationToken);

            user = await _context.Users.FirstAsync(item => item.Id == existingAccount.Id, cancellationToken);
            user.UpdateProfile(fullName, invitation.Email, BuildSyntheticStaffPhone(invitation.Id));
        }
        else
        {
            var createResult = await _identityAccountService.CreateAsync(
                new CreateIdentityAccountRequest(
                    fullName,
                    invitation.Email,
                    BuildSyntheticStaffPhone(invitation.Id),
                    UserRole.VendorStaff,
                    request.Password),
                cancellationToken);

            if (createResult.Status == IdentityCreateStatus.DuplicateEmailOrPhone)
            {
                return Conflict(new { code = "USER_ALREADY_EXISTS", message = ApiLocalizedMessages.Resolve(HttpContext, "STAFF_EMAIL_ALREADY_EXISTS") });
            }

            if (createResult.Status != IdentityCreateStatus.Succeeded || createResult.Account is null)
            {
                return BadRequest(new
                {
                    code = "IDENTITY_CREATE_FAILED",
                    message = ApiLocalizedMessages.Resolve(HttpContext, "IDENTITY_CREATE_FAILED")
                });
            }

            user = await _context.Users.FirstAsync(item => item.Id == createResult.Account.Id, cancellationToken);
        }

        user.VerifyEmail();
        user.UpdateDirectoryProfile("Vendor team", invitation.RoleTemplate);

        var (scopeType, scopeEntityId) = await ResolveScopeAsync(invitation, cancellationToken);
        var existingScope = await _context.UserAccessScopes
            .FirstOrDefaultAsync(item => item.UserId == user.Id && item.PanelScope == PanelScope.VendorPanel, cancellationToken);

        if (existingScope is not null)
        {
            existingScope.Update(
                role.Id,
                PanelScope.VendorPanel,
                scopeType,
                scopeEntityId,
                $"Accepted vendor staff invitation {invitation.Id}.");
            existingScope.Activate();
        }
        else
        {
            _context.UserAccessScopes.Add(new UserAccessScope(
                user.Id,
                role.Id,
                PanelScope.VendorPanel,
                scopeType,
                scopeEntityId,
                $"Accepted vendor staff invitation {invitation.Id}."));
        }

        var existingOverrides = await _context.UserPermissionOverrides
            .Where(item => item.UserId == user.Id)
            .ToListAsync(cancellationToken);
        if (existingOverrides.Count > 0)
        {
            _context.UserPermissionOverrides.RemoveRange(existingOverrides);
        }
        ApplyPermissionOverrides(user.Id, invitation.PermissionsJson, roleCode);
        user.IncrementPermissionVersion();
        invitation.Accept(user.Id, now);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = ApiLocalizedMessages.Resolve(HttpContext, "INVITATION_ACCEPTED_SUCCESS"),
            email = invitation.Email,
            redirectTo = "/login"
        });
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPut("staff/{id:guid}/role")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public async Task<IActionResult> UpdateStaffRole(
        Guid id,
        [FromBody] VendorStaffRoleUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var member = await RequireMutableStaffMemberAsync(scope, id, cancellationToken);
        var roleCode = ResolveRoleCode(request.RoleTemplate);
        var role = await _context.RoleDefinitions
            .Include(item => item.RolePermissions)
                .ThenInclude(item => item.PermissionDefinition)
            .FirstOrDefaultAsync(item => item.Code == roleCode && item.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("ROLE_NOT_CONFIGURED", $"Role {roleCode} is not configured.");

        var userScope = await _context.UserAccessScopes
            .FirstAsync(item => item.UserId == member.UserId && item.PanelScope == PanelScope.VendorPanel && item.IsActive, cancellationToken);
        userScope.Update(
            role.Id,
            PanelScope.VendorPanel,
            member.ScopeType,
            member.ScopeEntityId,
            member.Notes);

        if (request.Permissions is not null)
        {
            var existingOverrides = await _context.UserPermissionOverrides
                .Where(item => item.UserId == member.UserId)
                .ToListAsync(cancellationToken);
            if (existingOverrides.Count > 0)
            {
                _context.UserPermissionOverrides.RemoveRange(existingOverrides);
            }

            ApplyPermissionOverrides(member.UserId, JsonSerializer.Serialize(request.Permissions, JsonOptions), roleCode);
        }

        var user = await _context.Users.FirstAsync(item => item.Id == member.UserId, cancellationToken);
        user.UpdateDirectoryProfile(user.Department, request.RoleTemplate);
        user.IncrementPermissionVersion();
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(await FindStaffMemberAsync(scope, id, cancellationToken));
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPut("staff/{id:guid}/scope")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public async Task<IActionResult> UpdateStaffScope(
        Guid id,
        [FromBody] VendorStaffScopeUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var member = await RequireMutableStaffMemberAsync(scope, id, cancellationToken);
        await EnsureInvitationBranchesAsync(scope.VendorId, request.BranchIds, cancellationToken);
        EnsureBranchSelectionAllowed(request.BranchIds, scope);

        var branchId = Guid.Parse(request.BranchIds[0]);
        await Sender.Send(new UpdateUserScopeCommand(
            member.UserId,
            member.RoleDefinitionId,
            PanelScope.VendorPanel,
            AccessScopeType.VendorBranch,
            branchId,
            member.Notes), cancellationToken);

        return Ok(await FindStaffMemberAsync(scope, id, cancellationToken));
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPut("staff/{id:guid}/overrides")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public async Task<IActionResult> UpdateStaffOverrides(
        Guid id,
        [FromBody] VendorStaffOverridesUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var member = await RequireMutableStaffMemberAsync(scope, id, cancellationToken);
        var grantedPermissions = ResolveRequestedPermissionKeys(request.Permissions ?? new Dictionary<string, Dictionary<string, bool>>());
        grantedPermissions.UnionWith(PermissionKeys.Vendor.SessionBaseline);

        var defaultPermissions = ResolveDefaultPermissionKeys(member.RoleCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var grantedOverrides = grantedPermissions.Except(defaultPermissions, StringComparer.OrdinalIgnoreCase).ToList();
        var revokedOverrides = defaultPermissions.Except(grantedPermissions, StringComparer.OrdinalIgnoreCase).ToList();

        await Sender.Send(new UpdateUserOverridesCommand(member.UserId, grantedOverrides, revokedOverrides), cancellationToken);

        return Ok(await FindStaffMemberAsync(scope, id, cancellationToken));
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPut("staff/{id:guid}/status")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public async Task<IActionResult> UpdateStaffStatus(
        Guid id,
        [FromBody] VendorStaffStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var member = await RequireMutableStaffMemberAsync(scope, id, cancellationToken);
        var user = await _context.Users.FirstAsync(item => item.Id == member.UserId, cancellationToken);

        if (string.Equals(request.Status, "suspended", StringComparison.OrdinalIgnoreCase))
        {
            user.LockLogin("Suspended by vendor branch/staff administration.");
        }
        else
        {
            user.Activate();
            user.UnlockLogin();
        }

        user.IncrementPermissionVersion();
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(await FindStaffMemberAsync(scope, id, cancellationToken));
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpDelete("staff/{id:guid}")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public async Task<IActionResult> DeleteStaff(Guid id, CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        var member = await RequireMutableStaffMemberAsync(scope, id, cancellationToken);

        var relatedScopes = await _context.UserAccessScopes
            .Where(item => item.UserId == member.UserId && item.PanelScope == PanelScope.VendorPanel)
            .ToListAsync(cancellationToken);
        foreach (var userScope in relatedScopes)
        {
            userScope.Deactivate();
        }

        var overrides = await _context.UserPermissionOverrides
            .Where(item => item.UserId == member.UserId)
            .ToListAsync(cancellationToken);
        if (overrides.Count > 0)
        {
            _context.UserPermissionOverrides.RemoveRange(overrides);
        }

        var user = await _context.Users.FirstAsync(item => item.Id == member.UserId, cancellationToken);
        user.LockLogin("Vendor staff access removed.");
        user.IncrementPermissionVersion();

        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpGet("staff/{id:guid}/effective-access")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public async Task<IActionResult> GetStaffEffectiveAccess(Guid id, CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        await RequireMutableStaffMemberAsync(scope, id, cancellationToken);
        var result = await Sender.Send(new GetUserEffectiveAccessQuery(id), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPut("branches/{id:guid}/status")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public async Task<IActionResult> UpdateBranchStatus(
        Guid id,
        [FromBody] VendorBranchStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await RequireCompanyScopeForMutationAsync(cancellationToken);
        var branch = await _context.VendorBranches
            .Include(item => item.OperatingHours)
            .FirstOrDefaultAsync(item => item.Id == id && item.VendorId == scope.VendorId, cancellationToken)
            ?? throw new NotFoundException("VendorBranch", id);

        if (branch.IsPrimary && string.Equals(request.Status, "archived", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("PRIMARY_BRANCH_DELETE_FORBIDDEN", "The primary branch cannot be archived.");
        }

        if (string.Equals(request.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            branch.Activate();
        }
        else
        {
            await BranchActivePickupOrdersSupport.EnsureNoActivePickupOrdersAsync(_context, id, cancellationToken);
            branch.Deactivate();
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ToBranchResponse(branch));
    }

    private async Task<IReadOnlyList<VendorStaffResponse>> LoadStaffAsync(
        CurrentVendorScope currentScope,
        CancellationToken cancellationToken)
    {
        var members = await LoadStaffMembersAsync(currentScope, cancellationToken);

        return members.Select(ToStaffResponse).ToArray();
    }

    private async Task<VendorStaffResponse> FindStaffMemberAsync(
        CurrentVendorScope currentScope,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var member = await RequireMutableStaffMemberAsync(currentScope, userId, cancellationToken);
        return ToStaffResponse(member);
    }

    private async Task<StaffMemberProjection> RequireMutableStaffMemberAsync(
        CurrentVendorScope currentScope,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var members = await LoadStaffMembersAsync(currentScope, cancellationToken);
        var member = members.FirstOrDefault(item => item.UserId == userId);

        return member ?? throw new NotFoundException("VendorStaff", userId);
    }

    private async Task<List<StaffMemberProjection>> LoadStaffMembersAsync(
        CurrentVendorScope currentScope,
        CancellationToken cancellationToken)
    {
        var relevantBranches = await _context.VendorBranches
            .AsNoTracking()
            .Where(branch => branch.VendorId == currentScope.VendorId)
            .ToDictionaryAsync(branch => branch.Id, cancellationToken);

        var scopes = await _context.UserAccessScopes
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.RoleDefinition)
                .ThenInclude(role => role.RolePermissions)
                    .ThenInclude(rolePermission => rolePermission.PermissionDefinition)
            .Where(item => item.PanelScope == PanelScope.VendorPanel && item.IsActive)
            .ToListAsync(cancellationToken);

        var filteredScopes = scopes
            .Where(item =>
                item.User.Role == UserRole.VendorStaff &&
                ((item.ScopeType == AccessScopeType.VendorCompany && item.ScopeEntityId == currentScope.VendorId) ||
                 (item.ScopeType == AccessScopeType.VendorBranch &&
                  item.ScopeEntityId.HasValue &&
                  relevantBranches.ContainsKey(item.ScopeEntityId.Value))))
            .Where(item =>
                !currentScope.BranchId.HasValue ||
                (item.ScopeType == AccessScopeType.VendorBranch && item.ScopeEntityId == currentScope.BranchId.Value))
            .ToList();

        var userIds = filteredScopes.Select(item => item.UserId).Distinct().ToArray();
        var overrides = await _context.UserPermissionOverrides
            .AsNoTracking()
            .Where(item => userIds.Contains(item.UserId) && item.IsActive)
            .ToListAsync(cancellationToken);
        var overridesByUserId = overrides
            .GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var members = filteredScopes
            .Select(item =>
            {
                relevantBranches.TryGetValue(item.ScopeEntityId ?? Guid.Empty, out var branch);
                overridesByUserId.TryGetValue(item.UserId, out var userOverrides);
                return new StaffMemberProjection(
                    item.UserId,
                    item.User,
                    item,
                    branch,
                    item.RoleDefinition,
                    userOverrides ?? []);
            })
            .OrderBy(item => item.User.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return members;
    }

    private async Task<CurrentVendorScope> RequireCompanyScopeForMutationAsync(CancellationToken cancellationToken)
    {
        var scope = await _currentVendorService.GetRequiredVendorScopeAsync(cancellationToken);
        if (scope.BranchId.HasValue)
        {
            throw new UnauthorizedException("BRANCH_SCOPE_MUTATION_FORBIDDEN");
        }

        return scope;
    }

    private static void EnsureInvitationInScope(VendorStaffInvitation invitation, CurrentVendorScope scope)
    {
        if (!scope.BranchId.HasValue)
        {
            return;
        }

        var invitationBranchIds = ParseBranchIds(invitation.BranchIdsJson);
        if (!invitationBranchIds.Contains(scope.BranchId.Value))
        {
            throw new UnauthorizedException("INVITATION_SCOPE_FORBIDDEN");
        }
    }

    private static void EnsureBranchSelectionAllowed(IEnumerable<string> branchIds, CurrentVendorScope scope)
    {
        if (!scope.BranchId.HasValue)
        {
            return;
        }

        var selectedIds = branchIds
            .Select(item => Guid.TryParse(item, out var parsed) ? parsed : (Guid?)null)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToArray();

        if (selectedIds.Length != 1 || selectedIds[0] != scope.BranchId.Value)
        {
            throw new UnauthorizedException("BRANCH_SCOPE_MUTATION_FORBIDDEN");
        }
    }

    private async Task<Vendor> RequireVendorAsync(Guid vendorId, CancellationToken cancellationToken) =>
        await _context.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == vendorId, cancellationToken)
        ?? throw new NotFoundException("Vendor", vendorId);

    private async Task<VendorStaffInvitation> RequireInvitationAsync(
        Guid vendorId,
        Guid invitationId,
        CancellationToken cancellationToken) =>
        await _context.VendorStaffInvitations
            .FirstOrDefaultAsync(item => item.Id == invitationId && item.VendorId == vendorId, cancellationToken)
        ?? throw new NotFoundException("VendorStaffInvitation", invitationId);

    private async Task<VendorStaffInvitation> FindInvitationByTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new BusinessRuleException("INVITATION_TOKEN_REQUIRED", "Invitation token is required.");
        }

        var tokenHash = HashToken(token);
        return await _context.VendorStaffInvitations
            .FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken)
            ?? throw new NotFoundException("VendorStaffInvitation", tokenHash);
    }

    private async Task EnsureNotAlreadyStaffAsync(Guid vendorId, string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = VendorStaffInvitation.NormalizeEmail(email);
        var existingStaff = await (
            from user in _context.Users.AsNoTracking()
            join scope in _context.UserAccessScopes.AsNoTracking() on user.Id equals scope.UserId
            join branch in _context.VendorBranches.AsNoTracking() on scope.ScopeEntityId equals branch.Id into branchGroup
            from branch in branchGroup.DefaultIfEmpty()
            where user.Email == normalizedEmail &&
                  user.Role == UserRole.VendorStaff &&
                  scope.IsActive &&
                  scope.PanelScope == PanelScope.VendorPanel &&
                  ((scope.ScopeType == AccessScopeType.VendorCompany && scope.ScopeEntityId == vendorId) ||
                   (scope.ScopeType == AccessScopeType.VendorBranch && branch != null && branch.VendorId == vendorId))
            select user.Id)
            .AnyAsync(cancellationToken);

        if (existingStaff)
        {
            throw new BusinessRuleException("STAFF_ALREADY_EXISTS", "This email already has access to this vendor.");
        }
    }

    private async Task<HashSet<Guid>> GetLinkedVendorIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var companyVendorIds = await _context.UserAccessScopes
            .AsNoTracking()
            .Where(item =>
                item.UserId == userId &&
                item.PanelScope == PanelScope.VendorPanel &&
                item.ScopeType == AccessScopeType.VendorCompany &&
                item.ScopeEntityId.HasValue)
            .Select(item => item.ScopeEntityId!.Value)
            .ToListAsync(cancellationToken);

        var branchVendorIds = await (
            from scope in _context.UserAccessScopes.AsNoTracking()
            join branch in _context.VendorBranches.AsNoTracking() on scope.ScopeEntityId equals branch.Id
            where scope.UserId == userId &&
                  scope.PanelScope == PanelScope.VendorPanel &&
                  scope.ScopeType == AccessScopeType.VendorBranch
            select branch.VendorId)
            .ToListAsync(cancellationToken);

        return companyVendorIds
            .Concat(branchVendorIds)
            .ToHashSet();
    }

    private async Task ExpireDueInvitationsAsync(Guid vendorId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var due = await _context.VendorStaffInvitations
            .Where(item =>
                item.VendorId == vendorId &&
                (item.Status == VendorStaffInvitation.StatusPending ||
                 item.Status == VendorStaffInvitation.StatusDeliveryFailed) &&
                item.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return;
        }

        foreach (var invitation in due)
        {
            invitation.Expire(now);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<EmailSendResult> SendInvitationEmailAsync(
        Vendor vendor,
        VendorStaffInvitation invitation,
        string inviteLink,
        CancellationToken cancellationToken)
    {
        var vendorName = ResolveVendorName(vendor);
        var subject = $"Invitation to join {vendorName} on Zadana Vendor Panel";
        var textBody =
            $"You have been invited to join {vendorName} on Zadana vendor panel.\n" +
            $"Open this link before {invitation.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC:\n{inviteLink}";
        var html = BuildInvitationHtml(vendorName, invitation, inviteLink);

        var result = await _emailService.SendEmailAsync(
            new SendEmailRequest(
                [invitation.Email],
                subject,
                html,
                textBody,
                From: "Vendor Success Hub <hello@zadna0.com>",
                ReplyTo: string.IsNullOrWhiteSpace(vendor.OwnerEmail) ? vendor.ContactEmail : vendor.OwnerEmail,
                Metadata: new Dictionary<string, string>
                {
                    ["Event"] = "VendorStaffInvitation",
                    ["VendorId"] = vendor.Id.ToString(),
                    ["InvitationId"] = invitation.Id.ToString()
                }),
            cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Vendor staff invitation email failed. VendorId: {VendorId}, InvitationId: {InvitationId}, Reason: {Reason}",
                vendor.Id,
                invitation.Id,
                result.FailureReason);
        }

        return result;
    }

    private string BuildInviteLink(string token)
    {
        var baseUrl = ResolveVendorPanelBaseUrl();
        return $"{baseUrl}/invitations/accept/{Uri.EscapeDataString(token)}";
    }

    private string ResolveVendorPanelBaseUrl()
    {
        var configuredBaseUrl = _configuration["VendorPanel:PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl) &&
            !configuredBaseUrl.StartsWith("__SET_VIA_ENV__", StringComparison.OrdinalIgnoreCase))
        {
            return configuredBaseUrl.Trim().TrimEnd('/');
        }

        var host = Request.Host.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase))
        {
            return "http://localhost:4200";
        }

        return "https://zadana-vendor-panel.vercel.app";
    }

    private static string BuildInvitationHtml(VendorStaffInvitation invitation, string inviteLink) =>
        BuildInvitationHtml("Zadana", invitation, inviteLink, null);

    private string BuildInvitationHtml(string vendorName, VendorStaffInvitation invitation, string inviteLink)
    {
        var logoUrl = _configuration["Email:LogoUrl"];
        if (string.IsNullOrWhiteSpace(logoUrl) ||
            logoUrl.StartsWith("__SET_VIA_ENV__", StringComparison.OrdinalIgnoreCase))
        {
            logoUrl = "https://ik.imagekit.io/fnyx4x87z/logo/%D8%B4%D9%81%D8%A7%D9%81%20(4).png";
        }

        return BuildInvitationHtml(vendorName, invitation, inviteLink, logoUrl);
    }

    private static string BuildInvitationHtml(
        string vendorName,
        VendorStaffInvitation invitation,
        string inviteLink,
        string? logoUrl)
    {
        var safeVendor = WebUtility.HtmlEncode(vendorName);
        var safeName = WebUtility.HtmlEncode(invitation.TargetName);
        var safeLink = WebUtility.HtmlEncode(inviteLink);
        var expires = WebUtility.HtmlEncode(invitation.ExpiresAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'"));
        var safeLogo = WebUtility.HtmlEncode(
            string.IsNullOrWhiteSpace(logoUrl)
                ? "https://ik.imagekit.io/fnyx4x87z/logo/%D8%B4%D9%81%D8%A7%D9%81%20(4).png"
                : logoUrl.Trim());
        const string heroUrlEn = "https://ik.imagekit.io/fnyx4x87z/email_tamplet/staff-invite-hero-en.png";
        const string heroUrlAr = "https://ik.imagekit.io/fnyx4x87z/email_tamplet/staff-invite-hero-ar.png";

        return $$"""
            <div style="font-family:Arial,Tahoma,sans-serif;line-height:1.55;color:#132126;background:#edf7f8;padding:12px 8px">
              <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #c7e3e7;border-radius:10px;overflow:hidden">
                <div style="background:#007f92;padding:9px 12px;text-align:center">
                  <img src="{{safeLogo}}" width="72" alt="Zadna" style="display:block;width:72px;max-width:72px;height:auto;border:0;margin:0 auto" />
                </div>
                <div style="padding:18px 20px 18px">
                  <div style="max-width:440px;margin:0 auto 16px;border:1px solid #c7e3e7;border-radius:10px;overflow:hidden;background:#f7fbfc">
                    <img src="{{heroUrlEn}}" width="440" alt="Zadna vendor staff invitation" style="display:block;width:100%;max-width:440px;height:auto;border:0;margin:0 auto" />
                  </div>
                  <h1 style="margin:0 0 10px;color:#073843;font-size:18px;line-height:1.25">Vendor staff invitation</h1>
                  <p style="margin:0 0 12px;color:#405257;font-size:14px">Hello {{safeName}}, you have been invited to join <strong>{{safeVendor}}</strong> on the Zadna Vendor Panel.</p>
                  <p style="margin:0 0 16px;color:#405257;font-size:13px">This invitation is valid until {{expires}}. Open the link and create your password to activate access.</p>
                  <p style="text-align:center;margin:24px 0">
                    <a href="{{safeLink}}" style="display:inline-block;background:#007f92;color:#ffffff;text-decoration:none;border-radius:10px;padding:12px 20px;font-weight:700">Accept invitation</a>
                  </p>
                  <p style="margin:0 0 20px;color:#6a7c82;font-size:12px">If the button does not work, copy and paste this link into your browser:<br>{{safeLink}}</p>
                  <hr style="border:none;border-top:1px solid #e2e8f0;margin:20px 0" />
                  <div dir="rtl" style="font-family:Tahoma,Arial,sans-serif">
                    <div style="max-width:440px;margin:0 auto 16px;border:1px solid #c7e3e7;border-radius:10px;overflow:hidden;background:#f7fbfc">
                      <img src="{{heroUrlAr}}" width="440" alt="دعوة فريق تاجر زادنا" style="display:block;width:100%;max-width:440px;height:auto;border:0;margin:0 auto" />
                    </div>
                    <h2 style="margin:0 0 8px;color:#073843;font-size:18px;line-height:1.35">دعوة للانضمام لفريق التاجر</h2>
                    <p style="margin:0 0 12px;color:#405257;font-size:14px">أهلاً {{safeName}}، تمت دعوتك للانضمام إلى <strong>{{safeVendor}}</strong> في لوحة تاجر زادنا.</p>
                    <p style="margin:0 0 16px;color:#405257;font-size:13px">الدعوة سارية حتى {{expires}}. افتح الرابط وأنشئ كلمة المرور لتفعيل الوصول.</p>
                    <p style="text-align:center;margin:24px 0">
                      <a href="{{safeLink}}" style="display:inline-block;background:#007f92;color:#ffffff;text-decoration:none;border-radius:10px;padding:12px 20px;font-weight:700">قبول الدعوة</a>
                    </p>
                    <p style="margin:0;color:#6a7c82;font-size:12px">إذا لم يعمل الزر، انسخ الرابط والصقه في المتصفح:<br>{{safeLink}}</p>
                  </div>
                </div>
              </div>
            </div>
            """;
    }

    private static VendorStaffResponse ToStaffResponse(StaffMemberProjection member)
    {
        var effectivePermissions = BuildEffectivePermissions(member.RoleDefinition, member.Overrides);
        var roleTemplate = ResolveRoleTemplate(member.User.Team, member.RoleCode);
        var status = member.User.AccountStatus == AccountStatus.Active && !member.User.IsLoginLocked && member.Scope.IsActive
            ? "active"
            : "suspended";

        return new VendorStaffResponse(
            member.UserId,
            member.User.FullName,
            member.User.Email ?? string.Empty,
            status,
            roleTemplate,
            member.Scope.ScopeEntityId.HasValue && member.Scope.ScopeType == AccessScopeType.VendorBranch
                ? [member.Scope.ScopeEntityId.Value.ToString()]
                : [],
            member.User.LastSeenAtUtc ?? member.User.LastLoginAtUtc,
            member.RoleCode,
            member.RoleDefinition.Name,
            BuildPermissionMatrix(effectivePermissions));
    }

    private static HashSet<string> BuildEffectivePermissions(RoleDefinition role, IReadOnlyCollection<UserPermissionOverride> overrides)
    {
        var permissions = role.RolePermissions
            .Select(item => item.PermissionDefinition.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var overrideEntry in overrides)
        {
            if (overrideEntry.Mode == PermissionOverrideMode.Grant)
            {
                permissions.Add(overrideEntry.PermissionKey);
            }
            else
            {
                permissions.Remove(overrideEntry.PermissionKey);
            }
        }

        foreach (var permission in PermissionKeys.Vendor.SessionBaseline)
        {
            permissions.Add(permission);
        }

        return permissions;
    }

    private static Dictionary<string, Dictionary<string, bool>> BuildPermissionMatrix(IReadOnlySet<string> permissions)
    {
        var matrix = CreatePermissionMatrixSkeleton();
        foreach (var module in matrix.Keys)
        {
            foreach (var action in matrix[module].Keys.ToArray())
            {
                matrix[module][action] = MapUiPermission(module, action).Any(permissions.Contains);
            }
        }

        return matrix;
    }

    private static Dictionary<string, Dictionary<string, bool>> CreatePermissionMatrixSkeleton() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["dashboard"] = CreateActionState(["view", "manage", "approve", "export"]),
            ["products"] = CreateActionState(["view", "manage", "approve", "export"]),
            ["inventory"] = CreateActionState(["view", "manage", "approve", "export"]),
            ["orders"] = CreateActionState(["view", "manage", "approve", "export"]),
            ["offers"] = CreateActionState(["view", "manage", "approve", "export"]),
            ["branches_staff"] = CreateActionState(["view", "manage", "approve", "export"]),
            ["profile"] = CreateActionState(["view", "manage", "approve", "export"]),
            ["finance"] = CreateActionState(["view", "manage", "approve", "export"])
        };

    private static Dictionary<string, bool> CreateActionState(IEnumerable<string> actions) =>
        actions.ToDictionary(action => action, _ => false, StringComparer.OrdinalIgnoreCase);

    private static string ResolveRoleTemplate(string? team, string roleCode)
    {
        if (string.Equals(team, "branch_manager", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(team, "orders_clerk", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(team, "inventory_clerk", StringComparison.OrdinalIgnoreCase))
        {
            return team;
        }

        return string.Equals(roleCode, "vendor_branch_manager", StringComparison.OrdinalIgnoreCase)
            ? "branch_manager"
            : "orders_clerk";
    }

    private async Task<(AccessScopeType ScopeType, Guid ScopeEntityId)> ResolveScopeAsync(
        VendorStaffInvitation invitation,
        CancellationToken cancellationToken)
    {
        var branchIds = ParseBranchIds(invitation.BranchIdsJson);
        if (branchIds.Length > 0)
        {
            var validBranchIds = await _context.VendorBranches
                .AsNoTracking()
                .Where(branch => branch.VendorId == invitation.VendorId && branchIds.Contains(branch.Id))
                .Select(branch => branch.Id)
                .ToListAsync(cancellationToken);

            var validBranchSet = validBranchIds.ToHashSet();
            var branchId = branchIds.FirstOrDefault(validBranchSet.Contains);
            if (branchId != Guid.Empty)
            {
                return (AccessScopeType.VendorBranch, branchId);
            }

            throw new BusinessRuleException(
                "INVITATION_BRANCH_SCOPE_INVALID",
                "The invitation branch scope is no longer valid. Create the invitation again for an existing branch.");
        }

        return (AccessScopeType.VendorCompany, invitation.VendorId);
    }

    private async Task EnsureInvitationBranchesAsync(
        Guid vendorId,
        IReadOnlyCollection<string> branchIds,
        CancellationToken cancellationToken)
    {
        if (branchIds.Count != 1)
        {
            throw new BusinessRuleException(
                "SINGLE_BRANCH_SCOPE_REQUIRED",
                "Select exactly one branch for this invitation.");
        }

        var branchIdText = branchIds.First();
        if (!Guid.TryParse(branchIdText, out var branchId))
        {
            throw new BusinessRuleException(
                "INVALID_BRANCH_SCOPE",
                "Invitation branch id is invalid.");
        }

        var exists = await _context.VendorBranches
            .AsNoTracking()
            .AnyAsync(branch => branch.Id == branchId && branch.VendorId == vendorId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("VendorBranch", branchId);
        }
    }

    private void ApplyPermissionOverrides(Guid userId, string permissionsJson, string roleCode)
    {
        var requestedKeys = ResolveRequestedPermissionKeys(permissionsJson);
        if (requestedKeys.Count == 0)
        {
            return;
        }

        requestedKeys.UnionWith(PermissionKeys.Vendor.SessionBaseline);
        var defaultKeys = ResolveDefaultPermissionKeys(roleCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in requestedKeys.Except(defaultKeys, StringComparer.OrdinalIgnoreCase))
        {
            _context.UserPermissionOverrides.Add(new UserPermissionOverride(userId, key, PermissionOverrideMode.Grant));
        }

        foreach (var key in defaultKeys.Except(requestedKeys, StringComparer.OrdinalIgnoreCase))
        {
            _context.UserPermissionOverrides.Add(new UserPermissionOverride(userId, key, PermissionOverrideMode.Revoke));
        }
    }

    private static HashSet<string> ResolveRequestedPermissionKeys(Dictionary<string, Dictionary<string, bool>> permissions)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in permissions)
        {
            foreach (var action in module.Value)
            {
                if (!action.Value)
                {
                    continue;
                }

                foreach (var permission in MapUiPermission(module.Key, action.Key))
                {
                    keys.Add(permission);
                }
            }
        }

        return keys;
    }

    private static HashSet<string> ResolveRequestedPermissionKeys(string permissionsJson)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(permissionsJson))
        {
            return keys;
        }

        using var document = JsonDocument.Parse(permissionsJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return keys;
        }

        foreach (var module in document.RootElement.EnumerateObject())
        {
            if (module.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var action in module.Value.EnumerateObject())
            {
                if (action.Value.ValueKind != JsonValueKind.True)
                {
                    continue;
                }

                foreach (var permission in MapUiPermission(module.Name, action.Name))
                {
                    keys.Add(permission);
                }
            }
        }

        return keys;
    }

    private static IEnumerable<string> MapUiPermission(string module, string action) => (module, action) switch
    {
        ("dashboard", "view") => [PermissionKeys.Vendor.DashboardView],
        ("products", "view") => [PermissionKeys.Vendor.CatalogView],
        ("products", "manage") => [PermissionKeys.Vendor.CatalogCreate, PermissionKeys.Vendor.CatalogEdit],
        ("products", "approve") => [PermissionKeys.Vendor.CatalogApprove],
        ("products", "export") => [PermissionKeys.Vendor.CatalogExport],
        ("inventory", "view") => [PermissionKeys.Vendor.CatalogView],
        ("inventory", "manage") => [PermissionKeys.Vendor.CatalogEdit],
        ("inventory", "approve") => [PermissionKeys.Vendor.CatalogApprove],
        ("orders", "view") => [PermissionKeys.Vendor.OrdersView],
        ("orders", "manage") => [PermissionKeys.Vendor.OrdersEdit],
        ("orders", "approve") => [PermissionKeys.Vendor.OrdersApprove],
        ("orders", "export") => [PermissionKeys.Vendor.OrdersExport],
        ("offers", "view") => [PermissionKeys.Vendor.OffersView],
        ("offers", "manage") => [PermissionKeys.Vendor.OffersEdit],
        ("branches_staff", "view") => [PermissionKeys.Vendor.BranchTeamView, PermissionKeys.Vendor.StaffView],
        ("branches_staff", "manage") => [PermissionKeys.Vendor.BranchTeamCreate, PermissionKeys.Vendor.BranchTeamEdit, PermissionKeys.Vendor.StaffEdit],
        ("branches_staff", "approve") => [PermissionKeys.Vendor.BranchTeamApprove],
        ("profile", "view") => [PermissionKeys.Vendor.ProfileView],
        ("profile", "manage") => [PermissionKeys.Vendor.ProfileEdit],
        ("finance", "view") => [PermissionKeys.Vendor.FinanceView],
        ("finance", "manage") => [PermissionKeys.Vendor.FinanceEdit],
        ("finance", "export") => [PermissionKeys.Vendor.FinanceExport],
        _ => []
    };

    private static IEnumerable<string> ResolveDefaultPermissionKeys(string roleCode) => roleCode switch
    {
        "vendor_branch_manager" => PermissionKeys.Vendor.BranchManager,
        "vendor_branch_staff" => PermissionKeys.Vendor.BranchStaff,
        _ => PermissionKeys.Vendor.BranchStaff
    };

    private static VendorStaffInvitationResponse ToResponse(VendorStaffInvitation invitation, string? link) =>
        new(
            invitation.Id,
            invitation.Type,
            invitation.TargetName,
            invitation.Email,
            ReadBranchIds(invitation.BranchIdsJson),
            invitation.Status,
            invitation.SentAtUtc,
            invitation.ExpiresAtUtc,
            link ?? string.Empty,
            invitation.RoleTemplate,
            invitation.SendAttemptCount,
            invitation.ProviderMessageId,
            invitation.LastSendFailureReason);

    private static NormalizedInvitationRequest NormalizeCreateRequest(VendorStaffInvitationCreateRequest request)
    {
        var email = NormalizeEmailOrThrow(request.Contact);
        var type = VendorStaffInvitation.NormalizeType(request.Type);
        var targetName = string.IsNullOrWhiteSpace(request.TargetName)
            ? throw new BusinessRuleException("TARGET_NAME_REQUIRED", "Invitation target name is required.")
            : request.TargetName.Trim();
        var roleTemplate = VendorStaffInvitation.NormalizeRoleTemplate(request.RoleTemplate, type);
        var branchIds = (request.BranchIds ?? [])
            .Select(item => item?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();

        if (branchIds.Length == 0)
        {
            throw new BusinessRuleException("BRANCH_SCOPE_REQUIRED", "Select at least one branch for this invitation.");
        }

        return new NormalizedInvitationRequest(
            type,
            targetName,
            email,
            roleTemplate,
            branchIds,
            request.Permissions ?? new Dictionary<string, Dictionary<string, bool>>(),
            request.InviteMessage);
    }

    private static string NormalizeEmailOrThrow(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException("EMAIL_REQUIRED", "Email is required.");
        }

        try
        {
            return new MailAddress(value.Trim()).Address.ToLowerInvariant();
        }
        catch (FormatException)
        {
            throw new BusinessRuleException("INVALID_EMAIL", "Email address is invalid.");
        }
    }

    private static string[] ReadBranchIds(string branchIdsJson)
    {
        if (string.IsNullOrWhiteSpace(branchIdsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(branchIdsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Guid[] ParseBranchIds(string branchIdsJson) =>
        ReadBranchIds(branchIdsJson)
            .Select(item => Guid.TryParse(item, out var id) ? id : (Guid?)null)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToArray();

    private static string ResolveVendorName(Vendor vendor) =>
        !string.IsNullOrWhiteSpace(vendor.BusinessNameAr)
            ? vendor.BusinessNameAr
            : vendor.BusinessNameEn;

    private static string ResolveRoleCode(string roleTemplate) =>
        string.Equals(roleTemplate, "branch_manager", StringComparison.OrdinalIgnoreCase)
            ? "vendor_branch_manager"
            : "vendor_branch_staff";

    private static VendorBranchResponse ToBranchResponse(VendorBranch branch) =>
        new(
            branch.Id,
            branch.Name,
            branch.Code,
            branch.IsPrimary,
            branch.IsActive ? "active" : "suspended",
            branch.ContactPhone,
            branch.ManagerName,
            branch.ManagerContact,
            branch.Region,
            branch.City,
            branch.AddressLine,
            branch.Latitude,
            branch.Longitude,
            branch.DeliveryRadiusKm,
            branch.OperatingHours.Count(hour => !hour.IsClosed),
            branch.CreatedAtUtc,
            branch.OperatingHours
                .OrderBy(hour => DaySortIndex(hour.DayOfWeek))
                .Select(ToOperatingHourResponse)
                .ToArray());

    private static VendorBranchOperatingHourResponse ToOperatingHourResponse(BranchOperatingHour operatingHour) =>
        new(
            DayNumberToKey(operatingHour.DayOfWeek),
            operatingHour.OpenTime.ToString(@"hh\:mm"),
            operatingHour.CloseTime.ToString(@"hh\:mm"),
            !operatingHour.IsClosed);

    private static string GenerateBranchCode(VendorBranch branch)
    {
        return GenerateBranchCode(branch.Name);
    }

    private static string GenerateBranchCode(string branchName)
    {
        var normalized = new string(branchName
            .ToUpperInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray())
            .Trim('-');

        normalized = normalized.Length == 0 ? "BRANCH" : normalized[..Math.Min(normalized.Length, 12)];
        return normalized;
    }

    private static IEnumerable<BranchOperatingHour> NormalizeOperatingHours(
        Guid branchId,
        IEnumerable<VendorBranchOperatingHourRequest>? hours)
    {
        var requestedHours = (hours ?? [])
            .Select(hour => new BranchOperatingHour(
                branchId,
                DayKeyToNumber(hour.DayKey),
                ParseTime(hour.From),
                ParseTime(hour.To),
                !hour.IsOpen))
            .GroupBy(hour => hour.DayOfWeek)
            .Select(group => group.First())
            .ToDictionary(hour => hour.DayOfWeek);

        for (var day = 0; day <= 6; day++)
        {
            if (requestedHours.TryGetValue(day, out var operatingHour))
            {
                yield return operatingHour;
                continue;
            }

            yield return new BranchOperatingHour(branchId, day, TimeSpan.Zero, TimeSpan.Zero, isClosed: true);
        }
    }

    private static TimeSpan ParseTime(string? value) =>
        TimeSpan.TryParse(value, out var parsed) ? parsed : TimeSpan.Zero;

    private static string DayNumberToKey(int dayOfWeek) => dayOfWeek switch
    {
        0 => "SETTINGS_PROFILE.DAYS.SUNDAY",
        1 => "SETTINGS_PROFILE.DAYS.MONDAY",
        2 => "SETTINGS_PROFILE.DAYS.TUESDAY",
        3 => "SETTINGS_PROFILE.DAYS.WEDNESDAY",
        4 => "SETTINGS_PROFILE.DAYS.THURSDAY",
        5 => "SETTINGS_PROFILE.DAYS.FRIDAY",
        _ => "SETTINGS_PROFILE.DAYS.SATURDAY"
    };

    private static int DayKeyToNumber(string? dayKey) => dayKey switch
    {
        "SETTINGS_PROFILE.DAYS.SUNDAY" => 0,
        "SETTINGS_PROFILE.DAYS.MONDAY" => 1,
        "SETTINGS_PROFILE.DAYS.TUESDAY" => 2,
        "SETTINGS_PROFILE.DAYS.WEDNESDAY" => 3,
        "SETTINGS_PROFILE.DAYS.THURSDAY" => 4,
        "SETTINGS_PROFILE.DAYS.FRIDAY" => 5,
        "SETTINGS_PROFILE.DAYS.SATURDAY" => 6,
        _ => 6
    };

    private static int DaySortIndex(int dayOfWeek)
    {
        var order = new[] { 6, 0, 1, 2, 3, 4, 5 };
        var index = Array.IndexOf(order, dayOfWeek);
        return index < 0 ? order.Length : index;
    }

    private static string BuildSyntheticStaffPhone(Guid invitationId) => $"staff-{invitationId:N}"[..38];

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var normalized = token.Trim();
        var bytes = Encoding.UTF8.GetBytes(normalized);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private sealed record NormalizedInvitationRequest(
        string Type,
        string TargetName,
        string Email,
        string RoleTemplate,
        string[] BranchIds,
        Dictionary<string, Dictionary<string, bool>> Permissions,
        string? InviteMessage);
}

public sealed record VendorStaffInvitationCreateRequest(
    string Type,
    string TargetName,
    string Contact,
    string RoleTemplate,
    string[] BranchIds,
    Dictionary<string, Dictionary<string, bool>>? Permissions,
    string? InviteMessage);

public sealed record VendorStaffInvitationAcceptRequest(
    string Token,
    string Password,
    string? FullName);

public sealed record VendorStaffInvitationResponse(
    Guid Id,
    string Type,
    string TargetName,
    string Contact,
    string[] BranchIds,
    string Status,
    DateTime SentAt,
    DateTime ExpiresAt,
    string Link,
    string RoleTemplate,
    int SendAttemptCount,
    string? ProviderMessageId,
    string? LastSendFailureReason);

public sealed record VendorStaffInvitationAcceptancePreview(
    Guid Id,
    string Type,
    string TargetName,
    string Contact,
    string VendorName,
    string[] BranchIds,
    DateTime ExpiresAt);

public sealed record VendorBranchCreateRequest(
    string Name,
    string? Code,
    bool IsPrimary,
    string AddressLine,
    string? Phone,
    string ManagerName,
    string ManagerContact,
    string? Region,
    string? City,
    decimal? Latitude,
    decimal? Longitude,
    decimal? DeliveryRadiusKm,
    VendorBranchOperatingHourRequest[]? OperatingHours);

public sealed record VendorBranchUpdateRequest(
    string Name,
    string? Code,
    bool IsPrimary,
    string AddressLine,
    string? Phone,
    string ManagerName,
    string ManagerContact,
    string? Region,
    string? City,
    decimal? Latitude,
    decimal? Longitude,
    decimal? DeliveryRadiusKm,
    VendorBranchOperatingHourRequest[]? OperatingHours);

public sealed record VendorBranchStatusUpdateRequest(string Status);

public sealed record VendorBranchOperatingHourRequest(
    string DayKey,
    string From,
    string To,
    bool IsOpen);

public sealed record VendorStaffRoleUpdateRequest(
    string RoleTemplate,
    Dictionary<string, Dictionary<string, bool>>? Permissions = null);

public sealed record VendorStaffScopeUpdateRequest(string[] BranchIds);

public sealed record VendorStaffOverridesUpdateRequest(
    Dictionary<string, Dictionary<string, bool>>? Permissions);

public sealed record VendorStaffStatusUpdateRequest(string Status);

public sealed record VendorStaffResponse(
    Guid Id,
    string FullName,
    string Contact,
    string Status,
    string RoleTemplate,
    string[] BranchIds,
    DateTime? LastActiveAt,
    string RoleCode,
    string RoleName,
    Dictionary<string, Dictionary<string, bool>> Permissions);

public sealed record VendorBranchResponse(
    Guid Id,
    string Name,
    string Code,
    bool IsPrimary,
    string Status,
    string Phone,
    string ManagerName,
    string ManagerContact,
    string Region,
    string City,
    string AddressLine,
    decimal Latitude,
    decimal Longitude,
    decimal DeliveryRadiusKm,
    int WorkingDays,
    DateTime CreatedAt,
    VendorBranchOperatingHourResponse[] OperatingHours);

public sealed record VendorBranchOperatingHourResponse(
    string DayKey,
    string From,
    string To,
    bool IsOpen);

internal sealed record StaffMemberProjection(
    Guid UserId,
    User User,
    UserAccessScope Scope,
    VendorBranch? Branch,
    RoleDefinition RoleDefinition,
    IReadOnlyCollection<UserPermissionOverride> Overrides)
{
    public Guid RoleDefinitionId => Scope.RoleDefinitionId;
    public AccessScopeType ScopeType => Scope.ScopeType;
    public Guid? ScopeEntityId => Scope.ScopeEntityId;
    public string RoleCode => RoleDefinition.Code;
    public string? Notes => Scope.Notes;
}
