using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class Settlement : BaseEntity
{
    public Guid? VendorId { get; private set; }
    public Guid? DriverId { get; private set; }
    public SettlementOwnerType OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }
    public SettlementOrigin Origin { get; private set; }
    public SettlementStatus Status { get; private set; }
    public SettlementResolutionType ResolutionType { get; private set; }
    public DateTime PeriodFrom { get; private set; }
    public DateTime PeriodTo { get; private set; }
    
    public decimal GrossAmount { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public decimal RefundAmount { get; private set; }
    public decimal AdjustmentAmount { get; private set; }
    public decimal RecoveryAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    
    public DateTime? ProcessedAtUtc { get; private set; }

    // Navigation
    public Vendor? Vendor { get; private set; }
    public Driver? Driver { get; private set; }
    public ICollection<SettlementItem> Items { get; private set; } = [];
    public ICollection<Payout> Payouts { get; private set; } = [];

    private Settlement() { }

    public Settlement(Guid? vendorId, Guid? driverId, SettlementOrigin origin = SettlementOrigin.ManualBatch)
    {
        if (vendorId == null && driverId == null) 
            throw new InvalidOperationException("Settlement must be linked to either a Vendor or a Driver.");

        VendorId = vendorId;
        DriverId = driverId;
        OwnerType = vendorId.HasValue ? SettlementOwnerType.Vendor : SettlementOwnerType.Driver;
        OwnerId = vendorId ?? driverId!.Value;
        Origin = origin;
        Status = SettlementStatus.PendingReview;
        ResolutionType = SettlementResolutionType.BankPayout;
        PeriodFrom = DateTime.UtcNow.Date;
        PeriodTo = DateTime.UtcNow.Date;
        GrossAmount = 0;
        CommissionAmount = 0;
        RefundAmount = 0;
        AdjustmentAmount = 0;
        RecoveryAmount = 0;
        NetAmount = 0;
    }

    public Settlement(
        SettlementOwnerType ownerType,
        Guid ownerId,
        DateTime periodFrom,
        DateTime periodTo,
        SettlementOrigin origin = SettlementOrigin.ManualBatch)
        : this(
            ownerType == SettlementOwnerType.Vendor ? ownerId : null,
            ownerType == SettlementOwnerType.Driver ? ownerId : null,
            origin)
    {
        PeriodFrom = periodFrom;
        PeriodTo = periodTo;
    }

    public void UpdateTotals(
        decimal gross,
        decimal commission,
        decimal refund = 0,
        decimal adjustment = 0,
        decimal recovery = 0,
        SettlementResolutionType? resolutionType = null)
    {
        GrossAmount = gross;
        CommissionAmount = commission;
        RefundAmount = refund;
        AdjustmentAmount = adjustment;
        RecoveryAmount = recovery;
        NetAmount = gross - commission - refund + adjustment - recovery;
        ResolutionType = resolutionType ?? ResolveDefaultResolution(NetAmount);
    }

    public void Approve(SettlementResolutionType? resolutionType = null)
    {
        Status = SettlementStatus.Approved;
        ResolutionType = resolutionType ?? ResolveDefaultResolution(NetAmount);
    }

    public void Hold() => Status = SettlementStatus.OnHold;
    public void Reject() => Status = SettlementStatus.Rejected;
    public void Dispute() => Status = SettlementStatus.Disputed;
    public void ResolveDispute(SettlementResolutionType? resolutionType = null) => Approve(resolutionType);

    public void ApplyVendorRecovery(decimal amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (NetAmount < amount)
        {
            throw new InvalidOperationException("Settlement net amount cannot absorb this recovery.");
        }

        RecoveryAmount += amount;
        NetAmount -= amount;
        ResolutionType = ResolveDefaultResolution(NetAmount);
    }

    public void MarkAsProcessing() => Status = SettlementStatus.Processing;

    public void MarkAsSettled()
    {
        Status = SettlementStatus.Settled;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void MarkPaidOut()
    {
        Status = SettlementStatus.PaidOut;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void MarkPayoutFailed() => Status = SettlementStatus.PayoutFailed;
    public void MarkAsFailed() => Status = SettlementStatus.Failed;

    private static SettlementResolutionType ResolveDefaultResolution(decimal netAmount) =>
        netAmount > 0 ? SettlementResolutionType.BankPayout : SettlementResolutionType.NoTransferRequired;
}
