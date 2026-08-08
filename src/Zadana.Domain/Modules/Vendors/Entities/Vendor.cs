using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Social.Support;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;
using Zadana.SharedKernel.Security;

namespace Zadana.Domain.Modules.Vendors.Entities;

public class Vendor : BaseEntity
{
    public Guid UserId { get; private set; }
    public string BusinessNameAr { get; private set; } = null!;
    public string BusinessNameEn { get; private set; } = null!;
    public string BusinessType { get; private set; } = null!;
    public string CommercialRegistrationNumber { get; private set; } = null!;
    public string? CommercialRegistrationNumberHash { get; private set; }
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
    public PayoutScheduleDay PayoutDay { get; private set; } = PayoutScheduleDay.Monday;
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
    public string? TaxDocumentUrl { get; private set; }
    public string? LicenseDocumentUrl { get; private set; }
    public bool AcceptOrders { get; private set; } = true;
    public decimal? MinimumOrderAmount { get; private set; }
    public int? PreparationTimeMinutes { get; private set; }
    public bool EmailNotificationsEnabled { get; private set; } = true;
    public bool SmsNotificationsEnabled { get; private set; }
    public bool NewOrdersNotificationsEnabled { get; private set; } = true;
    public string NotificationSound { get; private set; } = NotificationSoundCatalog.Classic;

    public ICollection<VendorBranch> Branches { get; private set; } = [];
    public ICollection<VendorBankAccount> BankAccounts { get; private set; } = [];
    public ICollection<VendorDocumentReview> DocumentReviews { get; private set; } = [];
    public ICollection<VendorProfileReviewItem> ProfileReviewItems { get; private set; } = [];

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
        string? commercialRegisterDocumentUrl = null,
        string? taxDocumentUrl = null,
        string? licenseDocumentUrl = null,
        PayoutScheduleDay payoutDay = PayoutScheduleDay.Monday)
    {
        if (userId == Guid.Empty)
            throw new BusinessRuleException("INVALID_USER_ID", "User ID is required.");
        if (string.IsNullOrWhiteSpace(businessNameAr))
            throw new BusinessRuleException("INVALID_BUSINESS_NAME_AR", "Arabic business name is required.");
        if (string.IsNullOrWhiteSpace(businessNameEn))
            throw new BusinessRuleException("INVALID_BUSINESS_NAME_EN", "English business name is required.");
        if (string.IsNullOrWhiteSpace(businessType))
            throw new BusinessRuleException("INVALID_BUSINESS_TYPE", "Business type is required.");
        if (string.IsNullOrWhiteSpace(commercialRegistrationNumber))
            throw new BusinessRuleException("INVALID_CR_NUMBER", "Commercial registration number is required.");
        if (string.IsNullOrWhiteSpace(contactEmail))
            throw new BusinessRuleException("INVALID_CONTACT_EMAIL", "Contact email is required.");
        if (string.IsNullOrWhiteSpace(contactPhone))
            throw new BusinessRuleException("INVALID_CONTACT_PHONE", "Contact phone is required.");

        UserId = userId;
        BusinessNameAr = businessNameAr.Trim();
        BusinessNameEn = businessNameEn.Trim();
        BusinessType = businessType.Trim();
        CommercialRegistrationNumber = commercialRegistrationNumber.Trim();
        CommercialRegistrationNumberHash = ComputeCommercialRegistrationHash(CommercialRegistrationNumber);
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
        PayoutCycle = NormalizePayoutCycle(payoutCycle);
        FinancialLifecycleMode = ResolveFinancialLifecycleMode(PayoutCycle);
        PayoutDay = PayoutScheduleDayPolicy.EnsureAllowed(payoutDay);
        LogoUrl = logoUrl;
        CommercialRegisterDocumentUrl = commercialRegisterDocumentUrl;
        TaxDocumentUrl = NormalizeOptional(taxDocumentUrl);
        LicenseDocumentUrl = NormalizeOptional(licenseDocumentUrl);
        AcceptOrders = true;
        EmailNotificationsEnabled = true;
        NewOrdersNotificationsEnabled = true;
        NotificationSound = NotificationSoundCatalog.Classic;
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
        CommercialRegistrationNumberHash = ComputeCommercialRegistrationHash(CommercialRegistrationNumber);

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
        string? commercialRegisterDocumentUrl,
        string? taxDocumentUrl = null,
        string? licenseDocumentUrl = null)
    {
        CommercialRegistrationNumber = commercialRegistrationNumber.Trim();
        CommercialRegistrationNumberHash = ComputeCommercialRegistrationHash(CommercialRegistrationNumber);
        CommercialRegistrationExpiryDate = commercialRegistrationExpiryDate;
        TaxId = NormalizeOptional(taxId);
        LicenseNumber = NormalizeOptional(licenseNumber);

        if (!string.IsNullOrWhiteSpace(commercialRegisterDocumentUrl))
        {
            CommercialRegisterDocumentUrl = commercialRegisterDocumentUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(taxDocumentUrl))
        {
            TaxDocumentUrl = taxDocumentUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(licenseDocumentUrl))
        {
            LicenseDocumentUrl = licenseDocumentUrl.Trim();
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateBanking(string? payoutCycle, PayoutScheduleDay? payoutDay = null)
    {
        PayoutCycle = NormalizePayoutCycle(payoutCycle);
        FinancialLifecycleMode = ResolveFinancialLifecycleMode(PayoutCycle);

        if (payoutDay.HasValue)
        {
            PayoutDay = PayoutScheduleDayPolicy.EnsureAllowed(payoutDay.Value);
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateFinanceSettings(
        VendorFinancialLifecycleMode financialLifecycleMode,
        string? payoutCycle = null,
        PayoutScheduleDay? payoutDay = null)
    {
        // Per-order payouts are a legacy setting. New writes always fall back to
        // the scheduled weekly lifecycle so they cannot invoke a gateway payout.
        FinancialLifecycleMode = financialLifecycleMode == VendorFinancialLifecycleMode.PerOrderDirectPayout
            ? VendorFinancialLifecycleMode.Weekly
            : financialLifecycleMode;
        PayoutCycle = NormalizePayoutCycle(payoutCycle) ?? MapFinancialLifecycleModeToPayoutCycle(FinancialLifecycleMode);

        if (payoutDay.HasValue)
        {
            PayoutDay = PayoutScheduleDayPolicy.EnsureAllowed(payoutDay.Value);
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdatePayoutDay(PayoutScheduleDay payoutDay)
    {
        PayoutDay = PayoutScheduleDayPolicy.EnsureAllowed(payoutDay);
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
        bool newOrdersNotificationsEnabled,
        string? notificationSound = null)
    {
        EmailNotificationsEnabled = emailNotificationsEnabled;
        SmsNotificationsEnabled = smsNotificationsEnabled;
        NewOrdersNotificationsEnabled = newOrdersNotificationsEnabled;
        NotificationSound = notificationSound == null
            ? NotificationSound
            : NotificationSoundCatalog.Normalize(notificationSound, NotificationSound);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Approve(decimal commissionRate, Guid approvedBy)
    {
        if (Status == VendorStatus.Active)
            throw new BusinessRuleException("VendorAlreadyApproved", "التاجر معتمد بالفعل ولا يحتاج اعتمادًا جديدًا.|Vendor is already approved and does not need another approval.");

        if (ArchivedAtUtc.HasValue)
            throw new BusinessRuleException("VendorArchivedCannotBeApproved", "ما تقدر تعتمد تاجر مؤرشف. ألغِ الأرشفة أولاً.|Archived vendors cannot be approved. Restore the vendor first.");

        if (LockedAtUtc.HasValue)
            throw new BusinessRuleException("VendorLockedCannotBeApproved", "ما تقدر تعتمد تاجر مقفول الدخول. افتح القفل أولاً.|Locked vendors cannot be approved. Unlock the vendor first.");

        if (Status is not (VendorStatus.PendingReview or VendorStatus.Suspended))
            throw new BusinessRuleException("VendorInvalidStatusForApproval", $"ما تقدر تعتمد التاجر بينما حالته الحالية هي {Status}.|Vendor cannot be approved while its current status is {Status}.", Status);

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

    public void UpdateCommissionRate(decimal commissionRate)
    {
        if (Status != VendorStatus.Active)
            throw new BusinessRuleException("VendorNotActive", "ما تقدر تعدّل العمولة إلا لتاجر نشط.|Commission rate can only be updated for active vendors.");

        if (commissionRate < 0 || commissionRate > 100)
            throw new BusinessRuleException("InvalidCommissionRate", "Commission rate must be between 0 and 100.");

        CommissionRate = commissionRate;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reject(string reason)
    {
        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
            throw new BusinessRuleException("VendorRejectionReasonRequired", "لازم تدخل سبب رفض واضح.|A clear rejection reason is required.");

        if (Status == VendorStatus.Active || Status == VendorStatus.Suspended)
            throw new BusinessRuleException(
                "VendorInvalidStatusForRejection",
                "ما تقدر رفض تاجر معتمد أو معلق. استخدم التعليق لإيقاف الحساب التشغيلي.|Approved or suspended vendors cannot be rejected. Use suspension for operational shutdown.");

        if (Status != VendorStatus.PendingReview)
            throw new BusinessRuleException(
                "VendorInvalidStatusForRejection",
                $"ما تقدر رفض التاجر بينما حالته الحالية هي {Status}.|Vendor cannot be rejected while its current status is {Status}.",
                Status);

        Status = VendorStatus.Rejected;
        RejectionReason = normalizedReason;
        SuspensionReason = null;
        SuspendedAtUtc = null;
        LastStatusChangedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ReopenForReview()
    {
        if (Status != VendorStatus.Rejected)
        {
            throw new BusinessRuleException(
                "VendorInvalidStatusForReopen",
                "Only rejected vendors can reopen for review.");
        }

        Status = VendorStatus.PendingReview;
        RejectionReason = null;
        LastStatusChangedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Suspend(string reason)
    {
        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
            throw new BusinessRuleException("VendorSuspensionReasonRequired", "لازم تدخل سبب تعليق واضح.|A clear suspension reason is required.");

        if (Status != VendorStatus.Active)
            throw new BusinessRuleException(
                "VendorInvalidStatusForSuspension",
                $"ما تقدر تعليق الحساب بينما حالته الحالية هي {Status}.|Vendor cannot be suspended while its current status is {Status}.",
                Status);

        Status = VendorStatus.Suspended;
        SuspensionReason = normalizedReason;
        RejectionReason = null;
        SuspendedAtUtc = DateTime.UtcNow;
        LastStatusChangedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Lock(string reason)
    {
        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
            throw new BusinessRuleException("VendorLockReasonRequired", "لازم تدخل سبب قفل واضح.|A clear lock reason is required.");

        if (Status == VendorStatus.PendingReview)
        {
            throw new BusinessRuleException("VendorInvalidStatusForLock", $"Status: {Status}", Status);
        }

        LockReason = normalizedReason;
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
        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
            throw new BusinessRuleException("VendorArchiveReasonRequired", "لازم تدخل سبب أرشفة واضح.|A clear archive reason is required.");

        if (Status == VendorStatus.PendingReview || Status == VendorStatus.Rejected)
            throw new BusinessRuleException(
                "VendorInvalidStatusForArchive",
                $"ما تقدر تأرشف التاجر بينما حالته الحالية هي {Status}.|Vendor cannot be archived while its current status is {Status}.",
                Status);

        Status = VendorStatus.Suspended;
        ArchiveReason = normalizedReason;
        ArchivedAtUtc = DateTime.UtcNow;
        LockReason ??= normalizedReason;
        LockedAtUtc ??= DateTime.UtcNow;
        SuspensionReason ??= normalizedReason;
        SuspendedAtUtc ??= DateTime.UtcNow;
        LastStatusChangedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reactivate(Guid approvedBy)
    {
        if (Status != VendorStatus.Suspended)
            throw new BusinessRuleException(
                "VendorInvalidStatusForReactivation",
                $"ما تقدر تشغيل الحساب إلا إذا كان معلقًا. الحالة الحالية هي {Status}.|Vendor can only be reactivated when it is suspended. Current status is {Status}.",
                Status);

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

    private static string? ComputeCommercialRegistrationHash(string? value) =>
        SearchableHashProvider.Compute(value?.Trim().ToUpperInvariant());

    private static string? NormalizePayoutCycle(string? payoutCycle)
    {
        var normalized = NormalizeOptional(payoutCycle)?.ToLowerInvariant();

        return normalized is "per_order_direct_payout" or "perorderdirectpayout" or "per-order-direct-payout" or "order_by_order" or "orderbyorder"
            ? "weekly"
            : normalized;
    }

    private static VendorFinancialLifecycleMode ResolveFinancialLifecycleMode(string? payoutCycle)
    {
        var normalized = NormalizePayoutCycle(payoutCycle);

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
