namespace Zadana.Application.Common.Interfaces;

/// <summary>
/// Verifies that a proof reference came from the settlement-proof storage
/// channel.  The reference is deliberately opaque to finance workflows so the
/// implementation can later move from a storage URL to a durable attachment
/// identifier without weakening payout controls.
/// </summary>
public interface ISettlementProofReferenceValidator
{
    bool IsValid(string? proofReference);
}
