using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Vendors.Entities;

public class Vendor : BaseEntity
{
    public Guid UserId { get; private set; }
    public string BusinessNameAr { get; private set; } = null!;
    public string BusinessNameEn { get; private set; } = null!;
    public string BusinessType { get; private set; } = null!;
    public string CommercialRegistrationNumber { get; private set; } = null!;
    public string? TaxId { get; private set; }
    public string ContactEmail { get; private set; } = null!;
    public string ContactPhone { get; private set; } = null!;
    public string? DescriptionAr { get; private set; }
    public string? DescriptionEn { get; private set; }
    public string? OwnerName { get; private set; }
    public string? OwnerEmail { get; private set; }
    public string? OwnerPhone { get; private set; }
    public string? IdNumber { get; private set; }
    public string? Nationality { get; private set; }
    public string? Region { get; private set; }
    public string? City { get; private set; }
    public string? NationalAddress { get; private set; }
    public DateTime? CommercialRegistrationExpiryDate { get; private set; }
    public string? LicenseNumber { get; private set; }
    public string? PayoutCycle { get; private set; }
    public VendorFinancialLifecycleMode FinancialLifecycleMode { get; private set; }
    public decimal? CommissionRate { get; private set; }
    public VendorStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public string? ApprovalNote { get; private set; }
    public DateTime? SuspendedAtUtc { get; private set; }
    public string? SuspensionReason { get; private set; }
    public DateTime? LockedAtUtc { get; private set; }
    public string? LockReason { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public string? ArchiveReason { get; private set; }
    public DateTime? LastStatusChangedAtUtc { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? CommercialRegisterDocumentUrl { get; private set; }
    public bool AcceptOrders { get; private set; } = true;
    public decimal? MinimumOrderAmount { get; private set; }
    public int? PreparationTimeMinutes { get; private set; }
    public bool EmailNotificationsEnabled { get; private set; } = true;
    public bool SmsNotificationsEnabled { get; private set; }
    public bool NewOrdersNotificationsEnabled { get; private set; } = true;

    // Navigation
    public ICollection<VendorBranch> Branches { get; private set; } = [];
    public ICollection<VendorBankAccount> BankAccounts { get; private set; } = [];

    private Vendor() { }

    public Vendor(
        Guid userId,
        string businessNameAr,
        string businessNameEn,
        string businessType,
        string commercialRegistrationNumber,
        string contactEmail,
        string contactPhone,
        string? taxId = null,
        string? descriptionAr = null,
        string? descriptionEn = null,
        string? ownerName = null,
        string? ownerEmail = null,
        string? ownerPhone = null,
        string? idNumber = null,
        string? nationality = null,
        string? region = null,
        string? city = null,
        string? nationalAddress = null,
        DateTime? commercialRegistrationExpiryDate = null,
        string? licenseNumber = null,
        string? payoutCycle = null,
        string? logoUrl = null,
        string? commercialRegisterDocumentUrl = null)
    {
        UserId = userId;
        BusinessNameAr = businessNameAr.Trim();
        BusinessNameEn = businessNameEn.Trim();
        BusinessType = businessType.Trim();
        CommercialRegistrationNumber = commercialRegistrationNumber.Trim();
        ContactEmail = contactEmail.ToLowerInvariant().Trim();
        ContactPhone = contactPhone.Trim();
        DescriptionAr = NormalizeOptional(descriptionAr);
        DescriptionEn = NormalizeOptional(descriptionEn);
        OwnerName = NormalizeOptional(ownerName);
        OwnerEmail = NormalizeEmail(ownerEmail);
        OwnerPhone = NormalizeOptional(ownerPhone);
        IdNumber = NormalizeOptional(idNumber);
        Nationality = NormalizeOptional(nationality);
        Region = NormalizeOptional(region);
        City = NormalizeOptional(city);
        NationalAddress = NormalizeOptional(nationalAddress);
        CommercialRegistrationExpiryDate = commercialRegistrationExpiryDate;
        LicenseNumber = NormalizeOptional(licenseNumber);
        TaxId = taxId?.Trim();
        PayoutCycle = NormalizeOptional(payoutCycle);
        FinancialLifecycleMode = ResolveFinancialLifecycleMode(PayoutCycle);
        LogoUrl = logoUrl;
        CommercialRegisterDocumentUrl = commercialRegisterDocumentUrl;
        AcceptOrders = true;
        EmailNotificationsEnabled = true;
        NewOrdersNotificationsEnabled = true;
        Status = VendorStatus.PendingReview;
        LastStatusChangedAtUtc = DateTime.UtcNow;
    }

    public void UpdateProfile(
        string businessNameAr,
        string businessNameEn,
        string businessType,
        string contactEmail,
        string contactPhone,
        string? taxId)
    {
        BusinessNameAr = businessNameAr.Trim();
        BusinessNameEn = businessNameEn.Trim();
        BusinessType = businessType.Trim();
        ContactEmail = contactEmail.ToLowerInvariant().Trim();
        ContactPhone = contactPhone.Trim();
        TaxId = taxId?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateStore(
        string businessNameAr,
        string businessNameEn,
        string businessType,
        string contactEmail,
        string contactPhone,
        string? descriptionAr,
        string? descriptionEn,
        string? logoUrl,
        string? commercialRegisterDocumentUrl,
        string? region = null,
        string? city = null,
        string? nationalAddress = null,
        string? commercialRegistrationNumber = null)
    {
        BusinessNameAr = businessNameAr.Trim();
        BusinessNameEn = businessNameEn.Trim();
        BusinessType = businessType.Trim();
        ContactEmail = contactEmail.ToLowerInvariant().Trim();
        ContactPhone = contactPhone.Trim();
        DescriptionAr = NormalizeOptional(descriptionAr);
        DescriptionEn = NormalizeOptional(descriptionEn);
        Region = NormalizeOptional(region) ?? Region;
        City = NormalizeOptional(city) ?? City;
        NationalAddress = NormalizeOptional(nationalAddress) ?? NationalAddress;
        CommercialRegistrationNumber = string.IsNullOrWhiteSpace(commercialRegistrationNumber)
            ? CommercialRegistrationNumber
            : commercialRegistrationNumber.Trim();

        if (!string.IsNullOrWhiteSpace(logoUrl))
        {
            LogoUrl = logoUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(commercialRegisterDocumentUrl))
        {
            CommercialRegisterDocumentUrl = commercialRegisterDocumentUrl.Trim();
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateOwner(
        string ownerName,
        string ownerEmail,
        string ownerPhone,
        string? idNumber,
        string? nationality)
    {
        OwnerName = ownerName.Trim();
        OwnerEmail = ownerEmail.ToLowerInvariant().Trim();
        OwnerPhone = ownerPhone.Trim();
        IdNumber = NormalizeOptional(idNumber);
        Nationality = NormalizeOptional(nationality);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateContact(
        string region,
        string city,
        string nationalAddress)
    {
        Region = region.Trim();
        City = city.Trim();
        NationalAddress = nationalAddress.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateLegal(
        string commercialRegistrationNumber,
        DateTime? commercialRegistrationExpiryDate,
        string? taxId,
        string? licenseNumber,
        string? commercialRegisterDocumentUrl)
    {
        CommercialRegistrationNumber = commercialRegistrationNumber.Trim();
        CommercialRegistrationExpiryDate = commercialRegistrationExpiryDate;
        TaxId = NormalizeOptional(taxId);
        LicenseNumber = NormalizeOptional(licenseNumber);

        if (!string.IsNullOrWhiteSpace(commercialRegisterDocumentUrl))
        {
            CommercialRegisterDocumentUrl = commercialRegisterDocumentUrl.Trim();
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateBanking(string? payoutCycle)
    {
        PayoutCycle = NormalizeOptional(payoutCycle);
        FinancialLifecycleMode = ResolveFinancialLifecycleMode(PayoutCycle);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateFinanceSettings(VendorFinancialLifecycleMode financialLifecycleMode, string? payoutCycle = null)
    {
        FinancialLifecycleMode = financialLifecycleMode;

        if (financialLifecycleMode == VendorFinancialLifecycleMode.PerOrderDirectPayout)
        {
            PayoutCycle = null;
        }
        else
        {
            PayoutCycle = NormalizeOptional(payoutCycle) ?? MapFinancialLifecycleModeToPayoutCycle(financialLifecycleMode);
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateOperationsSettings(
        bool acceptOrders,
        decimal? minimumOrderAmount,
        int? preparationTimeMinutes)
    {
        if (minimumOrderAmount is < 0)
        {
            throw new BusinessRuleException("InvalidMinimumOrderAmount", string.Empty);
        }

        if (preparationTimeMinutes is < 0)
        {
            throw new BusinessRuleException("InvalidPreparationTimeMinutes", string.Empty);
        }

        AcceptOrders = acceptOrders;
        MinimumOrderAmount = minimumOrderAmount;
        PreparationTimeMinutes = preparationTimeMinutes;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateNotificationSettings(
        bool emailNotificationsEnabled,
        bool smsNotificationsEnabled,
        bool newOrdersNotificationsEnabled)
    {
        EmailNotificationsEnabled = emailNotificationsEnabled;
        SmsNotificationsEnabled = smsNotificationsEnabled;
        NewOrdersNotificationsEnabled = newOrdersNotificationsEnabled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Approve(decimal commissionRate, Guid approvedBy)
    {
        if (Status != VendorStatus.PendingReview)
            throw new BusinessRuleException("VendorInvalidStatusForApproval", $"Status: {Status}");

        if (commissionRate < 0 || commissionRate > 100)
            throw new BusinessRuleException("InvalidCommissionRate", string.Empty);

        Status = VendorStatus.Active;
        CommissionRate = commissionRate;
        ApprovedAtUtc = DateTime.UtcNow;
        ApprovedBy = approvedBy;
        ApprovalNote = null;
        RejectionReason = null;
        SuspensionReason = null;
        SuspendedAtUtc = null;
        LockReason = null;
        LockedAtUtc = null;
        ArchiveReason = null;
        ArchivedAtUtc = null;
        LastStatusChangedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reject(string reason)
    {
        if (Status != VendorStatus.PendingReview && Status != VendorStatus.Active)
            throw new BusinessRuleException("VendorInvalidStatusForRejection", $"Status: {Status}");

        Status = VendorStatus.Rejected;
        RejectionReason = reason.Trim();
        LastStatusChangedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Suspend(string reason)
    {
        if (Status != VendorStatus.Active)
            throw new BusinessRuleException("VendorInvalidStatusForSuspension", $"Status: {Status}");

        Status = VendorStatus.Suspended;
        SuspensionReason = reason.Trim();
        RejectionReason = reason.Trim();
        SuspendedAtUtc = DateTime.UtcNow;
        LastStatusChangedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Lock(string reason)
    {
        if (Status == VendorStatus.PendingReview)
        {
            throw new BusinessRuleException("VendorInvalidStatusForLock", $"Status: {Status}");
        }

        LockReason = reason.Trim();
        LockedAtUtc = DateTime.UtcNow;

        if (Status == VendorStatus.Active)
        {
            Status = VendorStatus.Suspended;
        }

        LastStatusChangedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Unlock()
    {
        LockReason = null;
        LockedAtUtc = null;

        if (Status == VendorStatus.Suspended
            && ArchivedAtUtc == null
            && string.IsNullOrWhiteSpace(SuspensionReason)
            && string.IsNullOrWhiteSpace(RejectionReason))
        {
            Status = VendorStatus.Active;
        }

        LastStatusChangedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Archive(string reason)
    {
        Status = VendorStatus.Suspended;
        ArchiveReason = reason.Trim();
        ArchivedAtUtc = DateTime.UtcNow;
        LockReason ??= reason.Trim();
        LockedAtUtc ??= DateTime.UtcNow;
        SuspensionReason ??= reason.Trim();
        SuspendedAtUtc ??= DateTime.UtcNow;
        LastStatusChangedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reactivate(Guid approvedBy)
    {
        if (Status != VendorStatus.Suspended)
            throw new BusinessRuleException("VendorInvalidStatusForReactivation", $"Status: {Status}");

        Status = VendorStatus.Active;
        RejectionReason = null;
        SuspensionReason = null;
        SuspendedAtUtc = null;
        LockReason = null;
        LockedAtUtc = null;
        ApprovedBy = approvedBy;
        ArchivedAtUtc = null;
        ArchiveReason = null;
        LastStatusChangedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.ToLowerInvariant().Trim();

    private static VendorFinancialLifecycleMode ResolveFinancialLifecycleMode(string? payoutCycle)
    {
        var normalized = NormalizeOptional(payoutCycle)?.ToLowerInvariant();

        return normalized switch
        {
            "biweekly" => VendorFinancialLifecycleMode.Biweekly,
            "monthly" => VendorFinancialLifecycleMode.Monthly,
            _ => VendorFinancialLifecycleMode.Weekly
        };
    }

    private static string MapFinancialLifecycleModeToPayoutCycle(VendorFinancialLifecycleMode mode) =>
        mode switch
        {
            VendorFinancialLifecycleMode.Biweekly => "biweekly",
            VendorFinancialLifecycleMode.Monthly => "monthly",
            _ => "weekly"
        };
}
