using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class DriverPayoutMethod : BaseEntity
{
    public Guid DriverId { get; private set; }
    public DriverPayoutMethodType MethodType { get; private set; }
    public string AccountHolderName { get; private set; } = null!;
    public string? ProviderName { get; private set; }
    public string AccountIdentifier { get; private set; } = null!;
    public string MaskedLabel { get; private set; } = null!;
    public bool IsPrimary { get; private set; }
    public bool IsVerified { get; private set; }

    private DriverPayoutMethod() { }

    public DriverPayoutMethod(
        Guid driverId,
        DriverPayoutMethodType methodType,
        string accountHolderName,
        string accountIdentifier,
        string? providerName = null,
        bool isPrimary = false)
    {
        DriverId = driverId;
        MethodType = methodType;
        AccountHolderName = accountHolderName.Trim();
        ProviderName = NormalizeOptional(providerName);
        AccountIdentifier = accountIdentifier.Trim();
        MaskedLabel = BuildMaskedLabel(methodType, ProviderName, AccountIdentifier);
        IsPrimary = isPrimary;
        IsVerified = true;
    }

    public void UpdateDetails(
        DriverPayoutMethodType methodType,
        string accountHolderName,
        string accountIdentifier,
        string? providerName = null)
    {
        MethodType = methodType;
        AccountHolderName = accountHolderName.Trim();
        ProviderName = NormalizeOptional(providerName);
        AccountIdentifier = accountIdentifier.Trim();
        MaskedLabel = BuildMaskedLabel(methodType, ProviderName, AccountIdentifier);
    }

    public void SetPrimary() => IsPrimary = true;

    public void UnsetPrimary() => IsPrimary = false;

    private static string BuildMaskedLabel(
        DriverPayoutMethodType methodType,
        string? providerName,
        string accountIdentifier)
    {
        var clean = new string(accountIdentifier.Where(char.IsLetterOrDigit).ToArray());
        var last4 = clean.Length <= 4 ? clean : clean[^4..];
        var prefix = methodType switch
        {
            DriverPayoutMethodType.BankAccount => providerName ?? "Bank account",
            DriverPayoutMethodType.DebitCard => providerName ?? "Debit card",
            DriverPayoutMethodType.InstantTransfer => providerName ?? "Instant transfer",
            _ => providerName ?? "Payout method"
        };

        return $"{prefix} ****{last4}";
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
