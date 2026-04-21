using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class Settlement : BaseEntity
{
    public Guid? VendorId { get; private set; }
    public Guid? DriverId { get; private set; }
    public SettlementOrigin Origin { get; private set; }
    public SettlementStatus Status { get; private set; }
    
    public decimal GrossAmount { get; private set; }
    public decimal CommissionAmount { get; private set; }
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
        Origin = origin;
        Status = SettlementStatus.Pending;
        GrossAmount = 0;
        CommissionAmount = 0;
        NetAmount = 0;
    }

    public void UpdateTotals(decimal gross, decimal commission)
    {
        GrossAmount = gross;
        CommissionAmount = commission;
        NetAmount = gross - commission;
    }

    public void MarkAsProcessing() => Status = SettlementStatus.Processing;

    public void MarkAsSettled()
    {
        Status = SettlementStatus.Settled;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsFailed() => Status = SettlementStatus.Failed;
}
