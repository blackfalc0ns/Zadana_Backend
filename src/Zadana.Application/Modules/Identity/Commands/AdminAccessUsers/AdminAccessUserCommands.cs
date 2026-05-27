using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Identity.Queries.GetAdminUsers;
using Zadana.Application.Modules.Identity.Services;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.AdminAccessUsers;

public record CreateAdminAccessUserCommand(
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
    string? Notes) : IRequest<AdminUserRecordDto>;

public record UpdateAdminAccessUserCommand(
    Guid UserId,
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
    DirectoryCommunicationProfileDto? Communication) : IRequest<AdminUserRecordDto>;

public record ResetAdminAccessUserTemporaryPasswordCommand(
    Guid UserId,
    string TemporaryPassword) : IRequest<AdminUserRecordDto>;

public sealed class CreateAdminAccessUserCommandHandler
    : IRequestHandler<CreateAdminAccessUserCommand, AdminUserRecordDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IAdminAccessValidationService _validationService;
    private readonly IAccessAuditService _auditService;
    private readonly IApplicationTransaction _transaction;
    private readonly IEmailVerificationSender _emailVerificationSender;

    public CreateAdminAccessUserCommandHandler(
        IApplicationDbContext context,
        IIdentityAccountService identityAccountService,
        IAdminAccessValidationService validationService,
        IAccessAuditService auditService,
        IApplicationTransaction transaction,
        IEmailVerificationSender emailVerificationSender)
    {
        _context = context;
        _identityAccountService = identityAccountService;
        _validationService = validationService;
        _auditService = auditService;
        _transaction = transaction;
        _emailVerificationSender = emailVerificationSender;
    }

    public async Task<AdminUserRecordDto> Handle(
        CreateAdminAccessUserCommand request,
        CancellationToken cancellationToken)
    {
        return await _transaction.ExecuteAsync(async ct =>
        {
            var role = await LoadActiveRoleAsync(_context, request.RoleDefinitionId, ct);

            var createResult = await _identityAccountService.CreateAsync(
                new CreateIdentityAccountRequest(
                    request.FullName,
                    request.Email,
                    request.Phone,
                    role.IdentityRole,
                    request.Password),
                ct);

            if (createResult.Status == IdentityCreateStatus.DuplicateEmailOrPhone)
            {
                throw new BusinessRuleException("USER_ALREADY_EXISTS", "Email or phone number is already in use.");
            }

            if (createResult.Status != IdentityCreateStatus.Succeeded || createResult.Account is null)
            {
                throw new BusinessRuleException(
                    "IDENTITY_CREATE_FAILED",
                    string.Join(", ", createResult.Errors ?? ["Unable to create the user account."]));
            }

            var user = await _context.Users.FindAsync([createResult.Account.Id], ct)
                ?? throw new NotFoundException(nameof(User), createResult.Account.Id);

            var normalizedScopeEntityId = await _validationService.NormalizeAndValidateScopeAsync(
                role,
                request.PanelScope,
                request.ScopeType,
                request.ScopeEntityId,
                user.Id,
                ct);

            user.UpdateDirectoryProfile(request.Department, request.Team);
            user.RequirePasswordChange();
            _context.UserAccessScopes.Add(new UserAccessScope(
                user.Id,
                role.Id,
                request.PanelScope,
                request.ScopeType,
                normalizedScopeEntityId,
                request.Notes));
            user.IncrementPermissionVersion();

            _auditService.Add(
                user.Id,
                "access-user-created",
                $"Access account created with role {role.Code}.",
                after: Snapshot(user, role, request.PanelScope, request.ScopeType, normalizedScopeEntityId, [], []));
            _auditService.Add(user.Id, "password-reset-required", "Temporary password requires first-login change.");

            await _context.SaveChangesAsync(ct);
            await _emailVerificationSender.SendAsync(user.Id, ct);
            return await ProjectUserAsync(_context, user.Id, ct);
        }, cancellationToken);
    }

    internal static async Task<RoleDefinition> LoadActiveRoleAsync(
        IApplicationDbContext context,
        Guid roleDefinitionId,
        CancellationToken cancellationToken)
    {
        var role = await context.RoleDefinitions
            .FirstOrDefaultAsync(x => x.Id == roleDefinitionId, cancellationToken);

        if (role is null)
        {
            throw new NotFoundException(nameof(RoleDefinition), roleDefinitionId);
        }

        if (!role.IsActive)
        {
            throw new BusinessRuleException("ROLE_INACTIVE", "The selected role is inactive.");
        }

        AccessRoleGuard.EnsureRoleMatchesPanelScope(role.IdentityRole, role.PanelScope);

        return role;
    }

    internal static async Task<AdminUserRecordDto> ProjectUserAsync(
        IApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var users = await AdminUserRecordProjector.BuildAsync(
            context,
            context.Users.Where(x => x.Id == userId),
            cancellationToken);

        return users.FirstOrDefault() ?? throw new NotFoundException(nameof(User), userId);
    }

    private static object Snapshot(
        User user,
        RoleDefinition role,
        PanelScope panelScope,
        AccessScopeType scopeType,
        Guid? scopeEntityId,
        IReadOnlyCollection<string> grantedPermissions,
        IReadOnlyCollection<string> revokedPermissions) => new
        {
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.Department,
            user.Team,
            user.Role,
            user.AccountStatus,
            RoleDefinitionId = role.Id,
            RoleCode = role.Code,
            PanelScope = panelScope.ToString(),
            ScopeType = scopeType.ToString(),
            ScopeEntityId = scopeEntityId,
            GrantedPermissions = grantedPermissions.OrderBy(x => x).ToArray(),
            RevokedPermissions = revokedPermissions.OrderBy(x => x).ToArray()
        };
}

public sealed class UpdateAdminAccessUserCommandHandler
    : IRequestHandler<UpdateAdminAccessUserCommand, AdminUserRecordDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAdminAccessValidationService _validationService;
    private readonly IAccessAuditService _auditService;
    private readonly IApplicationTransaction _transaction;
    private readonly IEmailVerificationSender _emailVerificationSender;

    public UpdateAdminAccessUserCommandHandler(
        IApplicationDbContext context,
        IIdentityAccountService identityAccountService,
        ICurrentUserService currentUserService,
        IAdminAccessValidationService validationService,
        IAccessAuditService auditService,
        IApplicationTransaction transaction,
        IEmailVerificationSender emailVerificationSender)
    {
        _context = context;
        _identityAccountService = identityAccountService;
        _currentUserService = currentUserService;
        _validationService = validationService;
        _auditService = auditService;
        _transaction = transaction;
        _emailVerificationSender = emailVerificationSender;
    }

    public async Task<AdminUserRecordDto> Handle(
        UpdateAdminAccessUserCommand request,
        CancellationToken cancellationToken)
    {
        return await _transaction.ExecuteAsync(async ct =>
        {
            var user = await _context.Users.FindAsync([request.UserId], ct)
                ?? throw new NotFoundException(nameof(User), request.UserId);
            var role = await CreateAdminAccessUserCommandHandler.LoadActiveRoleAsync(
                _context,
                request.RoleDefinitionId,
                ct);

            var grantedPermissions = request.GrantedPermissions ?? [];
            var revokedPermissions = request.RevokedPermissions ?? [];
            await _validationService.EnsureCanMutateUserAccessAsync(
                user.Id,
                user.Role,
                request.Status,
                _currentUserService.UserId,
                role,
                grantedPermissions,
                revokedPermissions,
                ct);
            var normalizedScopeEntityId = await _validationService.NormalizeAndValidateScopeAsync(
                role,
                request.PanelScope,
                request.ScopeType,
                request.ScopeEntityId,
                user.Id,
                ct);
            await _validationService.ValidatePermissionOverridesAsync(
                request.PanelScope,
                grantedPermissions,
                revokedPermissions,
                ct);

            var before = Snapshot(user, role, request.PanelScope, request.ScopeType, normalizedScopeEntityId, grantedPermissions, revokedPermissions);

            var profileResult = await _identityAccountService.UpdateProfileAsync(
                request.UserId,
                request.FullName,
                request.Email,
                request.Phone,
                ct);

            if (!profileResult.Succeeded)
            {
                throw new BusinessRuleException(
                    "IDENTITY_UPDATE_FAILED",
                    string.Join(", ", profileResult.Errors ?? ["Unable to update the user account."]));
            }

            var roleResult = await _identityAccountService.UpdateRoleAsync(
                request.UserId,
                role.IdentityRole,
                ct);

            if (!roleResult.Succeeded)
            {
                throw new BusinessRuleException(
                    "IDENTITY_ROLE_UPDATE_FAILED",
                    string.Join(", ", roleResult.Errors ?? ["Unable to update the identity role."]));
            }

            user = await _context.Users.FindAsync([request.UserId], ct)
                ?? throw new NotFoundException(nameof(User), request.UserId);

            user.UpdateDirectoryProfile(request.Department, request.Team);
            if (request.Communication is not null)
            {
                user.UpdateCommunicationProfile(
                    request.Communication.PreferredLocale,
                    request.Communication.ReplyTo,
                    request.Communication.NotificationEmails,
                    request.Communication.EscalationEmails,
                    request.Communication.EmailOptIn);
            }
            ApplyStatus(user, request.Status);
            await UpsertScopeAsync(user.Id, role.Id, request, normalizedScopeEntityId, ct);
            await ReplaceOverridesAsync(user.Id, grantedPermissions, revokedPermissions, ct);
            user.IncrementPermissionVersion();

            _auditService.Add(
                user.Id,
                "access-user-updated",
                $"Access account updated with role {role.Code}.",
                before,
                Snapshot(user, role, request.PanelScope, request.ScopeType, normalizedScopeEntityId, grantedPermissions, revokedPermissions));

            await _context.SaveChangesAsync(ct);
            if (profileResult.EmailChanged)
            {
                await _emailVerificationSender.SendAsync(user.Id, ct);
            }

            return await CreateAdminAccessUserCommandHandler.ProjectUserAsync(_context, user.Id, ct);
        }, cancellationToken);
    }

    private async Task UpsertScopeAsync(
        Guid userId,
        Guid roleDefinitionId,
        UpdateAdminAccessUserCommand request,
        Guid? normalizedScopeEntityId,
        CancellationToken cancellationToken)
    {
        var scope = await _context.UserAccessScopes
            .FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive, cancellationToken);

        if (scope is null)
        {
            _context.UserAccessScopes.Add(new UserAccessScope(
                userId,
                roleDefinitionId,
                request.PanelScope,
                request.ScopeType,
                normalizedScopeEntityId,
                request.Notes));
            return;
        }

        scope.Update(roleDefinitionId, request.PanelScope, request.ScopeType, normalizedScopeEntityId, request.Notes);
    }

    private async Task ReplaceOverridesAsync(
        Guid userId,
        IReadOnlyCollection<string> grantedPermissions,
        IReadOnlyCollection<string> revokedPermissions,
        CancellationToken cancellationToken)
    {
        var existing = await _context.UserPermissionOverrides
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        _context.UserPermissionOverrides.RemoveRange(existing);

        foreach (var permission in grantedPermissions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _context.UserPermissionOverrides.Add(new UserPermissionOverride(
                userId,
                permission,
                PermissionOverrideMode.Grant));
        }

        foreach (var permission in revokedPermissions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _context.UserPermissionOverrides.Add(new UserPermissionOverride(
                userId,
                permission,
                PermissionOverrideMode.Revoke));
        }
    }

    private static void ApplyStatus(User user, string? status)
    {
        switch (status?.Trim().ToLowerInvariant())
        {
            case "active":
                user.Activate();
                break;
            case "suspended":
                user.Suspend();
                break;
            case "inactive":
                user.Deactivate();
                break;
        }
    }

    private static object Snapshot(
        User user,
        RoleDefinition role,
        PanelScope panelScope,
        AccessScopeType scopeType,
        Guid? scopeEntityId,
        IReadOnlyCollection<string> grantedPermissions,
        IReadOnlyCollection<string> revokedPermissions) => new
        {
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.Department,
            user.Team,
            user.Role,
            user.AccountStatus,
            RoleDefinitionId = role.Id,
            RoleCode = role.Code,
            PanelScope = panelScope.ToString(),
            ScopeType = scopeType.ToString(),
            ScopeEntityId = scopeEntityId,
            GrantedPermissions = grantedPermissions.OrderBy(x => x).ToArray(),
            RevokedPermissions = revokedPermissions.OrderBy(x => x).ToArray()
        };
}

public sealed class ResetAdminAccessUserTemporaryPasswordCommandHandler
    : IRequestHandler<ResetAdminAccessUserTemporaryPasswordCommand, AdminUserRecordDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAccessAuditService _auditService;
    private readonly IApplicationTransaction _transaction;

    public ResetAdminAccessUserTemporaryPasswordCommandHandler(
        IApplicationDbContext context,
        IIdentityAccountService identityAccountService,
        ICurrentUserService currentUserService,
        IAccessAuditService auditService,
        IApplicationTransaction transaction)
    {
        _context = context;
        _identityAccountService = identityAccountService;
        _currentUserService = currentUserService;
        _auditService = auditService;
        _transaction = transaction;
    }

    public async Task<AdminUserRecordDto> Handle(
        ResetAdminAccessUserTemporaryPasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == _currentUserService.UserId)
        {
            throw new BusinessRuleException(
                "SELF_ACCESS_CHANGE_BLOCKED",
                "You cannot reset your own password from access management.");
        }

        if (string.IsNullOrWhiteSpace(request.TemporaryPassword))
        {
            throw new BusinessRuleException("PASSWORD_REQUIRED", "Temporary password is required.");
        }

        return await _transaction.ExecuteAsync(async ct =>
        {
            var user = await _context.Users.FindAsync([request.UserId], ct)
                ?? throw new NotFoundException(nameof(User), request.UserId);

            var result = await _identityAccountService.ResetPasswordByAdminAsync(
                request.UserId,
                request.TemporaryPassword,
                ct);

            if (!result.Succeeded)
            {
                throw new BusinessRuleException(
                    "PASSWORD_RESET_FAILED",
                    string.Join(", ", result.Errors ?? ["Unable to reset the password."]));
            }

            user = await _context.Users.FindAsync([request.UserId], ct)
                ?? throw new NotFoundException(nameof(User), request.UserId);
            user.RequirePasswordChange();
            user.IncrementPermissionVersion();

            _auditService.Add(
                user.Id,
                "temporary-password-reset",
                "Temporary password was reset by an administrator.",
                after: new
                {
                    user.Id,
                    user.Email,
                    user.MustChangePassword,
                    user.TemporaryPasswordIssuedAtUtc
                });

            await _context.SaveChangesAsync(ct);
            return await CreateAdminAccessUserCommandHandler.ProjectUserAsync(_context, user.Id, ct);
        }, cancellationToken);
    }
}
