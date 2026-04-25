using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class DriverWithdrawalRequest : BaseEntity
{
    public Guid DriverId { get; private set; }
    public Guid WalletId { get; private set; }
    public Guid DriverPayoutMethodId { get; private set; }
    public decimal Amount { get; private set; }
    public DriverWithdrawalStatus Status { get; private set; }
    public string? TransferReference { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    public Wallet Wallet { get; private set; } = null!;
    public DriverPayoutMethod DriverPayoutMethod { get; private set; } = null!;

    private DriverWithdrawalRequest() { }

    public DriverWithdrawalRequest(Guid driverId, Guid walletId, Guid driverPayoutMethodId, decimal amount)
    {
        if (amount <= 0)
        {
            throw new BusinessRuleException("INVALID_WITHDRAWAL_AMOUNT", "Withdrawal amount must be greater than zero.");
        }

        DriverId = driverId;
        WalletId = walletId;
        DriverPayoutMethodId = driverPayoutMethodId;
        Amount = amount;
        Status = DriverWithdrawalStatus.Pending;
    }

    public void MarkProcessing()
    {
        Status = DriverWithdrawalStatus.Processing;
    }

    public void MarkPaid(string? transferReference)
    {
        Status = DriverWithdrawalStatus.Paid;
        TransferReference = string.IsNullOrWhiteSpace(transferReference) ? null : transferReference.Trim();
        ProcessedAtUtc = DateTime.UtcNow;
        FailureReason = null;
    }

    public void MarkFailed(string? reason)
    {
        Status = DriverWithdrawalStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void Cancel(string? reason = null)
    {
        Status = DriverWithdrawalStatus.Cancelled;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ProcessedAtUtc = DateTime.UtcNow;
    }
}
