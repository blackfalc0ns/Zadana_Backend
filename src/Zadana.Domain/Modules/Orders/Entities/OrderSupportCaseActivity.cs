using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Orders.Entities;

public class OrderSupportCaseActivity : BaseEntity
{
    public Guid OrderSupportCaseId { get; private set; }
    public string Action { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Note { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string ActorRole { get; private set; } = null!;
    public bool VisibleToCustomer { get; private set; }
    public string MessageType { get; private set; } = null!;
    public string Audience { get; private set; } = null!;
    public bool IsInternalOnly { get; private set; }

    public OrderSupportCase OrderSupportCase { get; private set; } = null!;

    private OrderSupportCaseActivity()
    {
    }

    public OrderSupportCaseActivity(
        Guid orderSupportCaseId,
        string action,
        string title,
        string? note,
        Guid? actorUserId,
        string actorRole,
        bool visibleToCustomer,
        string messageType,
        string audience,
        bool isInternalOnly)
    {
        OrderSupportCaseId = orderSupportCaseId;
        Action = action.Trim();
        Title = title.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ActorUserId = actorUserId;
        ActorRole = NormalizeToken(actorRole) ?? "system";
        VisibleToCustomer = visibleToCustomer;
        MessageType = NormalizeToken(messageType) ?? "system";
        Audience = NormalizeAudience(audience, isInternalOnly);
        IsInternalOnly = isInternalOnly;
    }

    public bool IsVisibleToRole(string actorRole)
    {
        var normalizedRole = NormalizeToken(actorRole) ?? string.Empty;
        if (normalizedRole is "admin" or "superadmin")
        {
            return true;
        }

        if (string.Equals(normalizedRole, ActorRole, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsInternalOnly)
        {
            return false;
        }

        return Audience == "all_external" || Audience.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => string.Equals(token, normalizedRole, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> GetVisibleRoles()
    {
        if (IsInternalOnly)
        {
            return ["admin"];
        }

        if (string.Equals(Audience, "all_external", StringComparison.OrdinalIgnoreCase))
        {
            return ["customer", "vendor", "driver"];
        }

        return Audience
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeAudience(string audience, bool isInternalOnly)
    {
        if (isInternalOnly)
        {
            return "internal_admin_only";
        }

        var tokens = audience
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeToken)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return tokens.Count == 0 ? "all_external" : string.Join(',', tokens);
    }

    private static string? NormalizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
