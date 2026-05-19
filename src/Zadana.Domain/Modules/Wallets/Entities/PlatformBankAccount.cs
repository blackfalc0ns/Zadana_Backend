using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class PlatformBankAccount : BaseEntity
{
    public string BankName { get; private set; } = string.Empty;
    public string AccountHolderName { get; private set; } = string.Empty;
    public string IBAN { get; private set; } = string.Empty;
    public string? AccountNumber { get; private set; }
    public string CountryCode { get; private set; } = "SA";
    public string City { get; private set; } = "Riyadh";
    public bool IsActive { get; private set; }
    public bool IsBankTransferEnabled { get; private set; }
    public bool IsMoyasarPayoutsEnabled { get; private set; }
    public string? MoyasarPayoutSourceId { get; private set; }
    public string? Notes { get; private set; }

    private PlatformBankAccount() { }

    public PlatformBankAccount(
        string bankName,
        string accountHolderName,
        string iban,
        string? accountNumber,
        string countryCode,
        string city,
        bool isBankTransferEnabled,
        bool isMoyasarPayoutsEnabled,
        string? moyasarPayoutSourceId,
        string? notes = null)
    {
        IsActive = true;
        Update(
            bankName,
            accountHolderName,
            iban,
            accountNumber,
            countryCode,
            city,
            isBankTransferEnabled,
            isMoyasarPayoutsEnabled,
            moyasarPayoutSourceId,
            notes);
    }

    public void Update(
        string bankName,
        string accountHolderName,
        string iban,
        string? accountNumber,
        string countryCode,
        string city,
        bool isBankTransferEnabled,
        bool isMoyasarPayoutsEnabled,
        string? moyasarPayoutSourceId,
        string? notes = null)
    {
        BankName = Require(bankName, "PLATFORM_BANK_NAME_REQUIRED", "Platform bank name is required.");
        AccountHolderName = Require(accountHolderName, "PLATFORM_ACCOUNT_HOLDER_REQUIRED", "Platform account holder name is required.");
        IBAN = NormalizeIban(iban);
        AccountNumber = string.IsNullOrWhiteSpace(accountNumber) ? null : accountNumber.Trim();
        CountryCode = string.IsNullOrWhiteSpace(countryCode) ? "SA" : countryCode.Trim().ToUpperInvariant();
        City = string.IsNullOrWhiteSpace(city) ? "Riyadh" : city.Trim();
        IsBankTransferEnabled = isBankTransferEnabled;
        IsMoyasarPayoutsEnabled = isMoyasarPayoutsEnabled;
        MoyasarPayoutSourceId = string.IsNullOrWhiteSpace(moyasarPayoutSourceId) ? null : moyasarPayoutSourceId.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (CountryCode == "SA" && !IsSaudiIban(IBAN))
        {
            throw new BusinessRuleException("PLATFORM_IBAN_INVALID", "Platform IBAN must be a valid Saudi IBAN.");
        }

        if (IsMoyasarPayoutsEnabled && string.IsNullOrWhiteSpace(MoyasarPayoutSourceId))
        {
            throw new BusinessRuleException("MOYASAR_PAYOUT_SOURCE_REQUIRED", "Moyasar payout source id is required when payouts are enabled.");
        }
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static string Require(string value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(code, message);
        }

        return value.Trim();
    }

    private static string NormalizeIban(string iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            throw new BusinessRuleException("PLATFORM_IBAN_REQUIRED", "Platform IBAN is required.");
        }

        return new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    private static bool IsSaudiIban(string iban) =>
        iban.Length == 24 && iban.StartsWith("SA", StringComparison.OrdinalIgnoreCase);
}
