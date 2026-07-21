using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Finance;

public sealed class SettlementProcessingModeTests
{
    [Fact]
    public async Task SetModeAsync_persists_mode_and_records_the_administrator_change()
    {
        await using var context = TestDbContextFactory.Create();
        var service = new SettlementProcessingSettingsService(context);
        var adminId = Guid.NewGuid();

        var settings = await service.SetModeAsync(SettlementProcessingMode.Manual, adminId);

        settings.Mode.Should().Be(SettlementProcessingMode.Manual);
        settings.UpdatedByUserId.Should().Be(adminId);
        var audit = context.SettlementProcessingModeAudits.Single();
        audit.PreviousMode.Should().Be(SettlementProcessingMode.Automatic);
        audit.NewMode.Should().Be(SettlementProcessingMode.Manual);
        audit.ChangedByUserId.Should().Be(adminId);
    }

    [Fact]
    public async Task TriggerAsync_in_manual_mode_does_not_submit_or_retry_a_gateway_payout()
    {
        await using var context = TestDbContextFactory.Create();
        var settlement = new Settlement(Guid.NewGuid(), null);
        settlement.UpdateTotals(100m, 0m);
        var payout = new Payout(settlement.Id, 100m);
        context.Settlements.Add(settlement);
        context.Payouts.Add(payout);
        await context.SaveChangesAsync();

        var gateway = new Mock<IPayoutGateway>(MockBehavior.Strict);
        gateway.SetupGet(item => item.IsEnabled).Returns(true);
        var settings = new Mock<ISettlementProcessingSettingsService>(MockBehavior.Strict);
        settings
            .Setup(item => item.IsAutomaticAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var orchestrator = CreateOrchestrator(context, gateway.Object, settings.Object);

        await orchestrator.TriggerAsync(payout.Id);
        await orchestrator.TriggerAsync(payout.Id, isRetry: true);

        gateway.Verify(
            item => item.CreatePayoutAsync(It.IsAny<CreatePayoutCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        var storedPayout = context.Payouts.Single(item => item.Id == payout.Id);
        storedPayout.Status.Should().Be(PayoutStatus.Pending);
        context.PayoutAttempts.Should().BeEmpty();
    }

    private static PayoutOrchestrator CreateOrchestrator(
        IApplicationDbContext context,
        IPayoutGateway gateway,
        ISettlementProcessingSettingsService settings)
    {
        var postingService = new FinancialEventPostingService(
            context,
            NullLogger<FinancialEventPostingService>.Instance);
        var vendorPayoutWalletService = new VendorPayoutWalletService(
            context,
            NullLogger<VendorPayoutWalletService>.Instance);

        return new PayoutOrchestrator(
            context,
            [gateway],
            postingService,
            new WalletProjectionUpdater(context),
            vendorPayoutWalletService,
            Options.Create(new FinancialSettingsOptions()),
            Mock.Of<IAdminAlertService>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IOneSignalPushService>(),
            settings);
    }
}
