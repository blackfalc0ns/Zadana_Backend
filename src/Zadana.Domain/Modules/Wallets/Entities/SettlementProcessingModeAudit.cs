using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Domain.Modules.Wallets.Entities;

/// <summary>
/// Immutable history of changes to the global settlement processing mode.
/// </summary>
public sealed class SettlementProcessingModeAudit
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public SettlementProcessingMode PreviousMode { get; private set; }
    public SettlementProcessingMode NewMode { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; } = DateTime.UtcNow;

    private SettlementProcessingModeAudit() { }

    public SettlementProcessingModeAudit(
        SettlementProcessingMode previousMode,
        SettlementProcessingMode newMode,
        Guid changedByUserId)
    {
        PreviousMode = previousMode;
        NewMode = newMode;
        ChangedByUserId = changedByUserId;
        ChangedAtUtc = DateTime.UtcNow;
    }
}
