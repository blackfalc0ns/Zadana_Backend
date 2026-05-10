using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Finances;

public class LedgerWalletFlowTests
{
    [Fact]
    public async Task LedgerPostingProjectionAndRebuild_ShouldMoveBalancesAcrossVendorDriverPlatformAndCod()
    {
        await using var context = TestDbContextFactory.Create();
        var postingService = new FinancialEventPostingService(
            context,
            Mock.Of<ILogger<FinancialEventPostingService>>());
        var projectionUpdater = new WalletProjectionUpdater(context);

        var vendorId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var platformId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var onlineOrderId = Guid.NewGuid();
        var codOrderId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var settlementId = Guid.NewGuid();

        var onlinePosting = await postingService.PostAsync(
            FinancialEventType.OnlinePaymentDelivered,
            $"test-online:{onlineOrderId:N}",
            [
                new JournalLineDraft(FinancialAccountCode.GatewayReceivable, 115m, 0m, FinancialOwnerType.Gateway, OrderId: onlineOrderId),
                new JournalLineDraft(FinancialAccountCode.VendorPayable, 0m, 90m, FinancialOwnerType.Vendor, vendorId, onlineOrderId),
                new JournalLineDraft(FinancialAccountCode.DriverPayable, 0m, 10m, FinancialOwnerType.Driver, driverId, onlineOrderId),
                new JournalLineDraft(FinancialAccountCode.PlatformRevenue, 0m, 15m, FinancialOwnerType.Platform, platformId, onlineOrderId)
            ],
            orderId: onlineOrderId);
        await projectionUpdater.ApplyJournalEntryAsync(onlinePosting.JournalEntryId);

        var codPosting = await postingService.PostAsync(
            FinancialEventType.CodCashCollected,
            $"test-cod:{codOrderId:N}",
            [
                new JournalLineDraft(FinancialAccountCode.DriverCodReceivable, 115m, 0m, FinancialOwnerType.Driver, driverId, codOrderId),
                new JournalLineDraft(FinancialAccountCode.VendorPayable, 0m, 90m, FinancialOwnerType.Vendor, vendorId, codOrderId),
                new JournalLineDraft(FinancialAccountCode.DriverPayable, 0m, 10m, FinancialOwnerType.Driver, driverId, codOrderId),
                new JournalLineDraft(FinancialAccountCode.PlatformRevenue, 0m, 15m, FinancialOwnerType.Platform, platformId, codOrderId)
            ],
            orderId: codOrderId);
        await projectionUpdater.ApplyJournalEntryAsync(codPosting.JournalEntryId);

        var remittancePosting = await postingService.PostAsync(
            FinancialEventType.DriverCashRemittance,
            $"test-remittance:{driverId:N}",
            [
                new JournalLineDraft(FinancialAccountCode.PlatformCash, 115m, 0m, FinancialOwnerType.Platform, platformId),
                new JournalLineDraft(FinancialAccountCode.DriverCodReceivable, 0m, 115m, FinancialOwnerType.Driver, driverId)
            ]);
        await projectionUpdater.ApplyJournalEntryAsync(remittancePosting.JournalEntryId);

        var vendorPayoutPosting = await postingService.PostAsync(
            FinancialEventType.VendorPayoutPaid,
            $"test-vendor-payout:{payoutId:N}",
            [
                new JournalLineDraft(FinancialAccountCode.VendorPayable, 90m, 0m, FinancialOwnerType.Vendor, vendorId, SettlementId: settlementId, PayoutId: payoutId),
                new JournalLineDraft(FinancialAccountCode.PlatformCash, 0m, 90m, FinancialOwnerType.Platform, platformId, SettlementId: settlementId, PayoutId: payoutId)
            ],
            settlementId: settlementId,
            payoutId: payoutId);
        await projectionUpdater.ApplyJournalEntryAsync(vendorPayoutPosting.JournalEntryId);

        var vendorWallet = context.Wallets.Single(wallet => wallet.OwnerType == WalletOwnerType.Vendor && wallet.OwnerId == vendorId);
        var driverWallet = context.Wallets.Single(wallet => wallet.OwnerType == WalletOwnerType.Driver && wallet.OwnerId == driverId);
        var platformWallet = context.Wallets.Single(wallet => wallet.OwnerType == WalletOwnerType.Platform && wallet.OwnerId == platformId);

        vendorWallet.CurrentBalance.Should().Be(90m);
        driverWallet.CurrentBalance.Should().Be(20m);
        driverWallet.CodOwedBalance.Should().Be(0m);
        platformWallet.CurrentBalance.Should().Be(30m);

        var reportBeforeRebuild = await projectionUpdater.BuildReconciliationReportAsync();
        reportBeforeRebuild.IssueCount.Should().Be(0);

        var rebuildResult = await projectionUpdater.RebuildAllAsync();

        rebuildResult.JournalEntriesApplied.Should().Be(4);
        var reportAfterRebuild = await projectionUpdater.BuildReconciliationReportAsync();
        reportAfterRebuild.IssueCount.Should().Be(0);

        context.WalletTransactions.Count().Should().Be(9);
    }
}
