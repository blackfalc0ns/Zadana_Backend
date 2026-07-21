using Zadana.Domain.Modules.Wallets.Enums;
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
    public Guid? UpdatedByUserId { get; private set; }

    private SettlementProcessingSettings() { }

    public SettlementProcessingSettings(
        SettlementProcessingMode mode = SettlementProcessingMode.Automatic,
        Guid? updatedByUserId = null)
    {
        Id = SingletonId;
        SetMode(mode, updatedByUserId);
    }

    public void SetMode(SettlementProcessingMode mode, Guid? updatedByUserId)
    {
        Mode = mode;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
