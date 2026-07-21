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
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Serialization;
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

    [Fact]
    public async Task TriggerAsync_leaves_an_automatic_payout_pending_until_the_owner_selected_day()
    {
        await using var context = TestDbContextFactory.Create();
        var today = (PayoutScheduleDay)SaudiTime.Today.DayOfWeek;
        var selectedDay = Enum.GetValues<PayoutScheduleDay>().First(day => day != today);
        var vendor = new Vendor(
            Guid.NewGuid(),
            "متجر يوم التحويل",
            "Payout day vendor",
            "Retail",
            "CR-OFF-CYCLE",
            "off-cycle@example.test",
            "0500000000");
        vendor.UpdatePayoutDay(selectedDay);
        var settlement = new Settlement(vendor.Id, null);
        settlement.UpdateTotals(100m, 0m);
        settlement.Approve();
        var payout = new Payout(settlement.Id, 100m);
        context.Vendors.Add(vendor);
        context.Settlements.Add(settlement);
        context.Payouts.Add(payout);
        await context.SaveChangesAsync();

        var gateway = new Mock<IPayoutGateway>(MockBehavior.Strict);
        var settings = new Mock<ISettlementProcessingSettingsService>(MockBehavior.Strict);
        settings
            .Setup(item => item.IsAutomaticAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        settings
            .Setup(item => item.GetEnabledPayoutDaysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enum.GetValues<PayoutScheduleDay>());

        var orchestrator = CreateOrchestrator(context, gateway.Object, settings.Object);

        await orchestrator.TriggerAsync(payout.Id);

        context.Payouts.Single(item => item.Id == payout.Id).Status.Should().Be(PayoutStatus.Pending);
        context.PayoutAttempts.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_reassigns_removed_vendor_and_driver_days_to_a_deterministic_enabled_fallback()
    {
        await using var context = TestDbContextFactory.Create();
        var vendor = new Vendor(
            Guid.NewGuid(),
            "متجر اختبار",
            "Test vendor",
            "Retail",
            "CR-PAYOUT-DAYS",
            "vendor-payout-days@example.test",
            "0500000000");
        var driver = new Driver(Guid.NewGuid(), null, null, null);
        context.Vendors.Add(vendor);
        context.Drivers.Add(driver);
        await context.SaveChangesAsync();

        var service = new SettlementProcessingSettingsService(context);
        var settings = await service.UpdateAsync(
            SettlementProcessingMode.Automatic,
            [PayoutScheduleDay.Sunday, PayoutScheduleDay.Thursday],
            Guid.NewGuid());

        settings.GetPayoutDays().Should().Equal(PayoutScheduleDay.Sunday, PayoutScheduleDay.Thursday);
        vendor.PayoutDay.Should().Be(PayoutScheduleDay.Sunday);
        driver.PayoutDay.Should().Be(PayoutScheduleDay.Sunday);
    }

    [Fact]
    public async Task UpdateAsync_rejects_an_empty_enabled_day_list()
    {
        await using var context = TestDbContextFactory.Create();
        var service = new SettlementProcessingSettingsService(context);

        var action = () => service.UpdateAsync(
            SettlementProcessingMode.Manual,
            [],
            Guid.NewGuid());

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(error => error.ErrorCode == "PAYOUT_DAYS_REQUIRED");
    }

    [Fact]
    public async Task UpdateAsync_rejects_a_stale_settings_row_version()
    {
        await using var context = TestDbContextFactory.Create();
        var service = new SettlementProcessingSettingsService(context);
        await service.GetAsync();

        var action = () => service.UpdateAsync(
            SettlementProcessingMode.Manual,
            null,
            Guid.NewGuid(),
            expectedRowVersion: [1]);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(error => error.ErrorCode == "SETTLEMENT_PROCESSING_SETTINGS_CONFLICT");
    }

    [Fact]
    public async Task ResolveConfiguredPayoutDayAsync_uses_enabled_default_and_rejects_disabled_choice()
    {
        await using var context = TestDbContextFactory.Create();
        var service = new SettlementProcessingSettingsService(context);
        await service.UpdateAsync(
            SettlementProcessingMode.Automatic,
            [PayoutScheduleDay.Sunday],
            Guid.NewGuid());

        (await service.ResolveConfiguredPayoutDayAsync(null, PayoutScheduleDay.Monday))
            .Should().Be(PayoutScheduleDay.Sunday);

        var action = () => service.ResolveConfiguredPayoutDayAsync("Thursday", PayoutScheduleDay.Monday);
        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(error => error.ErrorCode == "PAYOUT_DAY_DISABLED");
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
