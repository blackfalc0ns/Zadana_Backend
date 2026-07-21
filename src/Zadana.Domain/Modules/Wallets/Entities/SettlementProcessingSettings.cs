using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

/// <summary>
/// Singleton, database-backed setting that controls whether newly due payouts
/// are submitted to the payout gateway or confirmed by finance manually.
/// </summary>
public sealed class SettlementProcessingSettings : BaseEntity
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public SettlementProcessingMode Mode { get; private set; } = SettlementProcessingMode.Automatic;
    /// <summary>
    /// Comma-separated enum names stored as a scalar for portable EF/SQL
    /// persistence. Use <see cref="GetPayoutDays"/> rather than consuming the
    /// storage representation directly.
    /// </summary>
    public string PayoutDays { get; private set; } = SerializePayoutDays(PayoutScheduleDayPolicy.DefaultPayoutDays);
    /// <summary>
    /// Production-safe default: the administrator who records that a bank
    /// transfer was submitted cannot also confirm the payout as paid.
    /// </summary>
    public bool RequireManualPayoutDualControl { get; private set; } = true;
    public Guid? UpdatedByUserId { get; private set; }
    /// <summary>
    /// SQL rowversion used with If-Match on finance settings updates.  A mode
    /// switch is a payment-control decision, so a stale admin screen must not
    /// silently overwrite a newer decision.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];

    private SettlementProcessingSettings() { }

    public SettlementProcessingSettings(
        SettlementProcessingMode mode = SettlementProcessingMode.Automatic,
        Guid? updatedByUserId = null,
        IEnumerable<PayoutScheduleDay>? payoutDays = null,
        bool requireManualPayoutDualControl = true)
    {
        Id = SingletonId;
        PayoutDays = SerializePayoutDays(
            PayoutScheduleDayPolicy.NormalizeDays(payoutDays, useDefaultWhenNull: true));
        RequireManualPayoutDualControl = requireManualPayoutDualControl;
        SetMode(mode, updatedByUserId);
    }

    public void SetMode(SettlementProcessingMode mode, Guid? updatedByUserId)
    {
        Mode = mode;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public IReadOnlyList<PayoutScheduleDay> GetPayoutDays()
    {
        var parsed = PayoutDays
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Enum.TryParse<PayoutScheduleDay>(value, ignoreCase: true, out var payoutDay)
                ? (PayoutScheduleDay?)payoutDay
                : null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        // Existing deployments may have a row created before PayoutDays was
        // introduced. Treat malformed/empty legacy values as the old default;
        // the next settings update persists the normalized value.
        return parsed.Length == 0 || parsed.Any(day => !PayoutScheduleDayPolicy.IsAllowed(day))
            ? PayoutScheduleDayPolicy.DefaultPayoutDays.ToArray()
            : PayoutScheduleDayPolicy.NormalizeDays(parsed);
    }

    public bool IsPayoutDayEnabled(PayoutScheduleDay payoutDay) =>
        GetPayoutDays().Contains(payoutDay);

    public void SetPayoutDays(IEnumerable<PayoutScheduleDay> payoutDays, Guid? updatedByUserId)
    {
        var normalized = PayoutScheduleDayPolicy.NormalizeDays(payoutDays);
        if (normalized.Count == 0)
        {
            throw new BusinessRuleException(
                "PAYOUT_DAYS_REQUIRED",
                "At least one payout day must be enabled.");
        }

        PayoutDays = SerializePayoutDays(normalized);
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetManualPayoutDualControl(bool required, Guid? updatedByUserId)
    {
        RequireManualPayoutDualControl = required;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string SerializePayoutDays(IEnumerable<PayoutScheduleDay> payoutDays) =>
        string.Join(
            ',',
            PayoutScheduleDayPolicy.NormalizeDays(payoutDays)
                .Select(day => day.ToString()));
}
