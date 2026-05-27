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
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
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
    public IActionResult GetBranches()
    {
        return Ok(Array.Empty<object>());
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpGet("staff")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public IActionResult GetStaff()
    {
        return Ok(Array.Empty<object>());
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpGet("staff/{id:guid}")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public IActionResult GetStaffMember(Guid id)
    {
        return NotFound();
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpGet("staff/invitations")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public async Task<IActionResult> GetInvitations(CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        await ExpireDueInvitationsAsync(vendorId, cancellationToken);

        var invitations = await _context.VendorStaffInvitations
            .AsNoTracking()
            .Where(invitation => invitation.VendorId == vendorId)
            .OrderByDescending(invitation => invitation.SentAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(invitations.Select(invitation => ToResponse(invitation, null)));
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpGet("staff/invitations/{id:guid}")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public async Task<IActionResult> GetInvitation(Guid id, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        await ExpireDueInvitationsAsync(vendorId, cancellationToken);

        var invitation = await _context.VendorStaffInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.VendorId == vendorId, cancellationToken)
            ?? throw new NotFoundException("VendorStaffInvitation", id);

        return Ok(ToResponse(invitation, null));
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPost("staff/invitations")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamCreate)]
    public async Task<IActionResult> CreateInvitation(
        [FromBody] VendorStaffInvitationCreateRequest request,
        CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var currentUserId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await RequireVendorAsync(vendorId, cancellationToken);
        var normalized = NormalizeCreateRequest(request);
        await EnsureNotAlreadyStaffAsync(vendorId, normalized.Email, cancellationToken);

        var now = DateTime.UtcNow;
        var token = GenerateToken();
        var tokenHash = HashToken(token);
        var expiresAt = now.Add(InvitationLifetime);
        var branchIdsJson = JsonSerializer.Serialize(normalized.BranchIds, JsonOptions);
        var permissionsJson = JsonSerializer.Serialize(normalized.Permissions, JsonOptions);

        var invitation = await _context.VendorStaffInvitations
            .FirstOrDefaultAsync(item =>
                item.VendorId == vendorId &&
                item.Email == normalized.Email &&
                item.Status != VendorStaffInvitation.StatusAccepted &&
                item.Status != VendorStaffInvitation.StatusRevoked,
                cancellationToken);

        if (invitation is null)
        {
            invitation = new VendorStaffInvitation(
                vendorId,
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
        return sendResult.Success ? Ok(response) : StatusCode(StatusCodes.Status502BadGateway, response);
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPost("staff/invitations/{id:guid}/resend")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public async Task<IActionResult> ResendInvitation(Guid id, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var vendor = await RequireVendorAsync(vendorId, cancellationToken);
        var invitation = await RequireInvitationAsync(vendorId, id, cancellationToken);

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
        return sendResult.Success ? Ok(response) : StatusCode(StatusCodes.Status502BadGateway, response);
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPost("staff/invitations/{id:guid}/revoke")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public async Task<IActionResult> RevokeInvitation(Guid id, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var invitation = await RequireInvitationAsync(vendorId, id, cancellationToken);
        invitation.Revoke(DateTime.UtcNow);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(invitation, null));
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

            return BadRequest(new { code = "INVITATION_NOT_ACTIVE", message = "Invitation is expired, accepted, or revoked." });
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
            return BadRequest(new { code = "WEAK_PASSWORD", message = "Password must be at least 8 characters." });
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

            return BadRequest(new { code = "INVITATION_NOT_ACTIVE", message = "Invitation is expired, accepted, or revoked." });
        }

        await EnsureNotAlreadyStaffAsync(invitation.VendorId, invitation.Email, cancellationToken);

        var roleCode = ResolveRoleCode(invitation.RoleTemplate);
        var role = await _context.RoleDefinitions
            .FirstOrDefaultAsync(item => item.Code == roleCode && item.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("ROLE_NOT_CONFIGURED", $"Role {roleCode} is not configured.");

        var fullName = string.IsNullOrWhiteSpace(request.FullName)
            ? invitation.TargetName
            : request.FullName.Trim();
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
            return Conflict(new { code = "USER_ALREADY_EXISTS", message = "An account already exists for this email." });
        }

        if (createResult.Status != IdentityCreateStatus.Succeeded || createResult.Account is null)
        {
            return BadRequest(new
            {
                code = "IDENTITY_CREATE_FAILED",
                message = string.Join(", ", createResult.Errors ?? ["Unable to create the staff account."])
            });
        }

        var user = await _context.Users.FirstAsync(item => item.Id == createResult.Account.Id, cancellationToken);
        user.VerifyEmail();
        user.UpdateDirectoryProfile("Vendor team", invitation.RoleTemplate);

        var (scopeType, scopeEntityId) = await ResolveScopeAsync(invitation, cancellationToken);
        _context.UserAccessScopes.Add(new UserAccessScope(
            user.Id,
            role.Id,
            PanelScope.VendorPanel,
            scopeType,
            scopeEntityId,
            $"Accepted vendor staff invitation {invitation.Id}."));

        ApplyPermissionOverrides(user.Id, invitation.PermissionsJson, roleCode);
        user.IncrementPermissionVersion();
        invitation.Accept(user.Id, now);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Invitation accepted.",
            email = invitation.Email,
            redirectTo = "/login"
        });
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPut("staff/{id:guid}/role")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public IActionResult UpdateStaffRole(Guid id, [FromBody] object request)
    {
        return Ok();
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPut("staff/{id:guid}/scope")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public IActionResult UpdateStaffScope(Guid id, [FromBody] object request)
    {
        return Ok();
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPut("staff/{id:guid}/overrides")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public IActionResult UpdateStaffOverrides(Guid id, [FromBody] object request)
    {
        return Ok();
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpGet("staff/{id:guid}/effective-access")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public IActionResult GetStaffEffectiveAccess(Guid id)
    {
        return Ok();
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
        BuildInvitationHtml("Zadana", invitation, inviteLink);

    private static string BuildInvitationHtml(string vendorName, VendorStaffInvitation invitation, string inviteLink)
    {
        var safeVendor = WebUtility.HtmlEncode(vendorName);
        var safeName = WebUtility.HtmlEncode(invitation.TargetName);
        var safeLink = WebUtility.HtmlEncode(inviteLink);
        var expires = WebUtility.HtmlEncode(invitation.ExpiresAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'"));

        return $$"""
            <div style="font-family:Arial,Tahoma,sans-serif;background:#f7fbfc;padding:28px;color:#0f172a">
              <div style="max-width:620px;margin:auto;background:#ffffff;border:1px solid #e2e8f0;border-radius:12px;padding:28px">
                <h1 style="margin:0 0 12px;font-size:24px;color:#0f766e">Vendor staff invitation</h1>
                <p style="font-size:16px;line-height:1.8;margin:0 0 14px">Hello {{safeName}}, you have been invited to join <strong>{{safeVendor}}</strong> on Zadana Vendor Panel.</p>
                <p style="font-size:14px;line-height:1.8;margin:0 0 20px;color:#475569">This invitation is valid until {{expires}}. Open the link and create your password to activate access.</p>
                <p style="text-align:center;margin:28px 0">
                  <a href="{{safeLink}}" style="display:inline-block;background:#0f766e;color:white;text-decoration:none;border-radius:10px;padding:13px 22px;font-weight:700">Accept invitation</a>
                </p>
                <p style="font-size:13px;line-height:1.7;color:#64748b;text-align:left">If the button does not work, copy and paste this link into your browser:<br>{{safeLink}}</p>
              </div>
            </div>
            """;
    }

    private async Task<(AccessScopeType ScopeType, Guid ScopeEntityId)> ResolveScopeAsync(
        VendorStaffInvitation invitation,
        CancellationToken cancellationToken)
    {
        var branchIds = ReadBranchIds(invitation.BranchIdsJson)
            .Select(item => Guid.TryParse(item, out var id) ? id : (Guid?)null)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToArray();

        if (branchIds.Length == 1)
        {
            var branchId = branchIds[0];
            var branchExists = await _context.VendorBranches
                .AsNoTracking()
                .AnyAsync(branch => branch.Id == branchId && branch.VendorId == invitation.VendorId, cancellationToken);

            if (branchExists)
            {
                return (AccessScopeType.VendorBranch, branchId);
            }
        }

        return (AccessScopeType.VendorCompany, invitation.VendorId);
    }

    private void ApplyPermissionOverrides(Guid userId, string permissionsJson, string roleCode)
    {
        var requestedKeys = ResolveRequestedPermissionKeys(permissionsJson);
        if (requestedKeys.Count == 0)
        {
            return;
        }

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

    private static string ResolveVendorName(Vendor vendor) =>
        !string.IsNullOrWhiteSpace(vendor.BusinessNameAr)
            ? vendor.BusinessNameAr
            : vendor.BusinessNameEn;

    private static string ResolveRoleCode(string roleTemplate) =>
        string.Equals(roleTemplate, "branch_manager", StringComparison.OrdinalIgnoreCase)
            ? "vendor_branch_manager"
            : "vendor_branch_staff";

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
