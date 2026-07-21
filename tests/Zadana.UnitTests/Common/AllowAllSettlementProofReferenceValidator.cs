using Zadana.Application.Common.Interfaces;

namespace Zadana.UnitTests.Common;

/// <summary>
/// Finance workflow tests supply a deterministic validator; storage-path
/// validation itself is covered at the API boundary.
/// </summary>
public sealed class AllowAllSettlementProofReferenceValidator : ISettlementProofReferenceValidator
{
    public bool IsValid(string? proofReference) => true;
}
