namespace Zadana.Domain.Modules.Wallets.Enums;

public enum PayoutAttemptType
{
    Trigger,
    Retry,
    ProviderCallback,
    ManualConfirmation,
    Cancel
}
