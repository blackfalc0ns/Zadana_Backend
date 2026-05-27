using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Vendors.Entities;

public class VendorStaffInvitation : BaseEntity
{
    public const string TypeBranchManager = "branch_manager";
    public const string TypeEmployee = "employee";

    public const string StatusPending = "pending";
    public const string StatusAccepted = "accepted";
    public const string StatusExpired = "expired";
    public const string StatusRevoked = "revoked";
    public const string StatusDeliveryFailed = "delivery_failed";

    public Guid VendorId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? AcceptedUserId { get; private set; }
    public string Type { get; private set; } = TypeEmployee;
    public string TargetName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string RoleTemplate { get; private set; } = TypeEmployee;
    public string BranchIdsJson { get; private set; } = "[]";
    public string PermissionsJson { get; private set; } = "{}";
    public string TokenHash { get; private set; } = null!;
    public string Status { get; private set; } = StatusPending;
    public string? InviteMessage { get; private set; }
    public DateTime SentAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public int SendAttemptCount { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? LastSendFailureReason { get; private set; }

    public Vendor Vendor { get; private set; } = null!;

    private VendorStaffInvitation() { }

    public VendorStaffInvitation(
        Guid vendorId,
        Guid createdByUserId,
        string type,
        string targetName,
        string email,
        string roleTemplate,
        string branchIdsJson,
        string permissionsJson,
        string tokenHash,
        DateTime sentAtUtc,
        DateTime expiresAtUtc,
        string? inviteMessage)
    {
        VendorId = vendorId;
        CreatedByUserId = createdByUserId;
        Type = NormalizeType(type);
        TargetName = targetName.Trim();
        Email = NormalizeEmail(email);
        RoleTemplate = NormalizeRoleTemplate(roleTemplate, Type);
        BranchIdsJson = string.IsNullOrWhiteSpace(branchIdsJson) ? "[]" : branchIdsJson.Trim();
        PermissionsJson = string.IsNullOrWhiteSpace(permissionsJson) ? "{}" : permissionsJson.Trim();
        TokenHash = tokenHash.Trim();
        SentAtUtc = sentAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        InviteMessage = string.IsNullOrWhiteSpace(inviteMessage) ? null : inviteMessage.Trim();
        Status = StatusPending;
        CreatedAtUtc = sentAtUtc;
        UpdatedAtUtc = sentAtUtc;
    }

    public void RefreshDetails(
        string type,
        string targetName,
        string roleTemplate,
        string branchIdsJson,
        string permissionsJson,
        string? inviteMessage,
        string tokenHash,
        DateTime sentAtUtc,
        DateTime expiresAtUtc)
    {
        Type = NormalizeType(type);
        TargetName = targetName.Trim();
        RoleTemplate = NormalizeRoleTemplate(roleTemplate, Type);
        BranchIdsJson = string.IsNullOrWhiteSpace(branchIdsJson) ? "[]" : branchIdsJson.Trim();
        PermissionsJson = string.IsNullOrWhiteSpace(permissionsJson) ? "{}" : permissionsJson.Trim();
        InviteMessage = string.IsNullOrWhiteSpace(inviteMessage) ? null : inviteMessage.Trim();
        RotateToken(tokenHash, sentAtUtc, expiresAtUtc);
    }

    public void RotateToken(string tokenHash, DateTime sentAtUtc, DateTime expiresAtUtc)
    {
        TokenHash = tokenHash.Trim();
        SentAtUtc = sentAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Status = StatusPending;
        RevokedAtUtc = null;
        LastSendFailureReason = null;
        ProviderMessageId = null;
        UpdatedAtUtc = sentAtUtc;
    }

    public void MarkSendResult(bool success, string? providerMessageId, string? failureReason)
    {
        SendAttemptCount++;
        ProviderMessageId = success ? providerMessageId : null;
        LastSendFailureReason = success ? null : failureReason;
        Status = success ? StatusPending : StatusDeliveryFailed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Revoke(DateTime nowUtc)
    {
        if (Status == StatusAccepted)
        {
            return;
        }

        Status = StatusRevoked;
        RevokedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Expire(DateTime nowUtc)
    {
        if (Status != StatusPending && Status != StatusDeliveryFailed)
        {
            return;
        }

        Status = StatusExpired;
        UpdatedAtUtc = nowUtc;
    }

    public void Accept(Guid userId, DateTime nowUtc)
    {
        Status = StatusAccepted;
        AcceptedUserId = userId;
        AcceptedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public bool CanBeAccepted(DateTime nowUtc) =>
        (Status == StatusPending || Status == StatusDeliveryFailed) &&
        RevokedAtUtc is null &&
        AcceptedAtUtc is null &&
        ExpiresAtUtc > nowUtc;

    public static string NormalizeType(string value) =>
        string.Equals(value?.Trim(), TypeBranchManager, StringComparison.OrdinalIgnoreCase)
            ? TypeBranchManager
            : TypeEmployee;

    public static string NormalizeRoleTemplate(string value, string type)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "branch_manager" => "branch_manager",
            "orders_clerk" => "orders_clerk",
            "inventory_clerk" => "inventory_clerk",
            _ when NormalizeType(type) == TypeBranchManager => "branch_manager",
            _ => "orders_clerk"
        };
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
