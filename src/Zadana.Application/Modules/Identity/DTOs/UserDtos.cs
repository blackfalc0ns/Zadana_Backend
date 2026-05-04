using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.DTOs;

public record AdminUserSecurityDto(
    bool MfaEnabled,
    string? LastLoginAt,
    string InvitedBy,
    string? InvitedAt,
    string? AcceptedAt,
    string VerificationState
);

public record DirectoryAssignmentDto(
    string? EntityId,
    string EntitySource,
    string? VendorId,
    string VendorName,
    string? BranchId,
    string BranchName,
    string Region,
    string City
);

public record DirectoryCommunicationProfileDto(
    string PrimaryEmail,
    List<string> NotificationEmails,
    string ReplyTo,
    List<string> EscalationEmails,
    string PreferredLocale,
    object EmailOptIn
);

public record AdminUserRecordDto(
    Guid Id,
    string? EntityId,
    string Source,
    string FullName,
    string Email,
    string Phone,
    string Department,
    string Team,
    string PersonaType,
    string AudienceType,
    string IdentityKind,
    string PanelScope,
    string RolePresetId,
    string AccessLevel,
    string Status,
    string InviteState,
    List<string> GrantedPermissions,
    List<string> RevokedPermissions,
    AdminUserSecurityDto Security,
    string AvatarHue,
    DirectoryAssignmentDto Assignment,
    DirectoryCommunicationProfileDto Communication,
    List<string> FeatureToggles,
    string EntityPath,
    List<string> Tags
);
