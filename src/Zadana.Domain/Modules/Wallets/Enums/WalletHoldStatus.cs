namespace Zadana.Domain.Modules.Wallets.Enums;

/// <summary>
/// Lifecycle state of a wallet hold. A hold is born <see cref="Active"/>,
/// then transitions to <see cref="Released"/>, <see cref="Consumed"/>,
/// <see cref="Expired"/>, or <see cref="Cancelled"/>.
/// </summary>
public enum WalletHoldStatus
{
    Active = 0,
    Released = 1,
    Consumed = 2,
    Expired = 3,
    Cancelled = 4,
}
