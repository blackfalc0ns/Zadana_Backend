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
    Guid? RoleDefinitionId,
    string RoleCode,
    string RoleName,
    List<string> RolePermissions,
    string RolePresetId,
    string AccessLevel,
    string Status,
    string InviteState,
    bool MustChangePassword,
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

public record PagedResultDto<T>(
    List<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);

public record AccessAuditLogDto(
    Guid Id,
    Guid? ActorUserId,
    string? ActorFullName,
    string? ActorEmail,
    Guid TargetUserId,
    string Action,
    string Summary,
    string? BeforeJson,
    string? AfterJson,
    string CreatedAtUtc,
    string? IpAddress,
    string? UserAgent);

public record SystemLogEntryDto(
    Guid Id,
    string OccurredAtUtc,
    string SourceApp,
    string Module,
    string Action,
    string Summary,
    string RequestPath,
    string HttpMethod,
    int StatusCode,
    bool IsSuccess,
    Guid? ActorUserId,
    string? ActorFullName,
    string? ActorEmail,
    string? ActorRole,
    string? TargetEntityType,
    string? TargetEntityId,
    string? CorrelationId,
    string? IpAddress,
    string? UserAgent,
    string? QueryString,
    string? RequestPayloadJson,
    string? MetadataJson,
    string? ErrorMessage);
