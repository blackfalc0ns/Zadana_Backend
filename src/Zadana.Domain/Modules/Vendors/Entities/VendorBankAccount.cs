using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Vendors.Entities;

public class VendorBankAccount : BaseEntity
{
    public Guid VendorId { get; private set; }
    public string BankName { get; private set; } = null!;
    public string AccountHolderName { get; private set; } = null!;
    public string IBAN { get; private set; } = null!;
    public string? SwiftCode { get; private set; }
    public bool IsPrimary { get; private set; }
    public BankAccountStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }
    public Guid? VerifiedBy { get; private set; }

    // Navigation
    public Vendor Vendor { get; private set; } = null!;

    private VendorBankAccount() { }

    public VendorBankAccount(
        Guid vendorId,
        string bankName,
        string accountHolderName,
        string iban,
        string? swiftCode = null)
    {
        if (string.IsNullOrWhiteSpace(bankName))
            throw new BusinessRuleException("INVALID_BANK_NAME", "Bank name is required.");
        if (string.IsNullOrWhiteSpace(accountHolderName))
            throw new BusinessRuleException("INVALID_ACCOUNT_HOLDER", "Account holder name is required.");
        ValidateIban(iban);

        VendorId = vendorId;
        BankName = bankName.Trim();
        AccountHolderName = accountHolderName.Trim();
        IBAN = iban.Trim().ToUpperInvariant();
        SwiftCode = swiftCode?.Trim().ToUpperInvariant();
        IsPrimary = false;
        Status = BankAccountStatus.PendingVerification;
    }

    public void UpdateDetails(
        string bankName,
        string accountHolderName,
        string iban,
        string? swiftCode = null)
    {
        if (string.IsNullOrWhiteSpace(bankName))
            throw new BusinessRuleException("INVALID_BANK_NAME", "Bank name is required.");
        if (string.IsNullOrWhiteSpace(accountHolderName))
            throw new BusinessRuleException("INVALID_ACCOUNT_HOLDER", "Account holder name is required.");
        ValidateIban(iban);

        BankName = bankName.Trim();
        AccountHolderName = accountHolderName.Trim();
        IBAN = iban.Trim().ToUpperInvariant();
        SwiftCode = swiftCode?.Trim().ToUpperInvariant();
        Status = BankAccountStatus.PendingVerification;
        RejectionReason = null;
        VerifiedAtUtc = null;
        VerifiedBy = null;
    }

    public void Verify(Guid verifiedBy)
    {
        if (Status != BankAccountStatus.PendingVerification)
            throw new BusinessRuleException("BANK_INVALID_STATUS", $"Can only verify from PendingVerification. Current: {Status}");

        Status = BankAccountStatus.Verified;
        VerifiedAtUtc = DateTime.UtcNow;
        VerifiedBy = verifiedBy;
        RejectionReason = null;
    }

    public void Reject(string reason)
    {
        if (Status != BankAccountStatus.PendingVerification)
            throw new BusinessRuleException("BANK_INVALID_STATUS", $"Can only reject from PendingVerification. Current: {Status}");

        Status = BankAccountStatus.Rejected;
        RejectionReason = reason;
    }

    public void SetAsPrimary()
    {
        if (Status != BankAccountStatus.Verified)
            throw new BusinessRuleException("BANK_NOT_VERIFIED", "Only verified bank accounts can be set as primary.");

        IsPrimary = true;
    }

    public void UnsetPrimary() => IsPrimary = false;

    public void MarkAsPreferredForSetup()
    {
        // During initial setup, allow setting as primary without verification
        IsPrimary = true;
    }

    private static void ValidateIban(string iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            throw new BusinessRuleException("INVALID_IBAN", "IBAN is required.");

        var normalized = iban.Trim().ToUpperInvariant().Replace(" ", "");
        if (normalized.Length < 15 || normalized.Length > 34)
            throw new BusinessRuleException("INVALID_IBAN_LENGTH", "IBAN must be between 15 and 34 characters.");

        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[A-Z]{2}[0-9]{2}[A-Z0-9]+$"))
            throw new BusinessRuleException("INVALID_IBAN_FORMAT", "IBAN format is invalid. Must start with 2 letters followed by 2 digits.");
    }
}
