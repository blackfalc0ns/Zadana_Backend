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
    public decimal? CommissionRate { get; private set; }
    public VendorStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? CommercialRegisterDocumentUrl { get; private set; }

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
        TaxId = taxId?.Trim();
        LogoUrl = logoUrl;
        CommercialRegisterDocumentUrl = commercialRegisterDocumentUrl;
        Status = VendorStatus.PendingReview;
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
        RejectionReason = null;
    }

    public void Reject(string reason)
    {
        if (Status != VendorStatus.PendingReview)
            throw new BusinessRuleException("VendorInvalidStatusForRejection", $"Status: {Status}");

        Status = VendorStatus.Rejected;
        RejectionReason = reason;
    }

    public void Suspend(string reason)
    {
        if (Status != VendorStatus.Active)
            throw new BusinessRuleException("VendorInvalidStatusForSuspension", $"Status: {Status}");

        Status = VendorStatus.Suspended;
        RejectionReason = reason;
    }

    public void Reactivate(Guid approvedBy)
    {
        if (Status != VendorStatus.Suspended)
            throw new BusinessRuleException("VendorInvalidStatusForReactivation", $"Status: {Status}");

        Status = VendorStatus.Active;
        RejectionReason = null;
        ApprovedBy = approvedBy;
    }
}
