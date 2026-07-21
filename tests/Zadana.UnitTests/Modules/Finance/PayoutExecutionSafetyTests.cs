using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;
using Zadana.SharedKernel.Serialization;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Finance;

public sealed class PayoutExecutionSafetyTests
{
    [Fact]
    public async Task Manual_claim_remains_a_gateway_barrier_after_mode_switch_to_automatic()
    {
        await using var context = TestDbContextFactory.Create();
        var owner = CreateVendorForToday();
        var settlement = CreateApprovedSettlement(owner);
        var payout = new Payout(settlement.Id, 100m);
        context.Vendors.Add(owner);
        context.Settlements.Add(settlement);
        context.Payouts.Add(payout);
        await context.SaveChangesAsync();

        var settings = new SettlementProcessingSettingsService(context);
        var firstAdmin = Guid.NewGuid();
        await settings.UpdateAsync(
            SettlementProcessingMode.Manual,
            [(PayoutScheduleDay)SaudiTime.Today.DayOfWeek],
            firstAdmin);

        var gateway = new Mock<IPayoutGateway>(MockBehavior.Strict);
        gateway.SetupGet(item => item.IsEnabled).Returns(true);
        gateway.SetupGet(item => item.ProviderName).Returns("TestGateway");
        var orchestrator = CreateOrchestrator(context, [gateway.Object], settings);

        await orchestrator.ClaimManualAsync(payout.Id, firstAdmin);
        await settings.SetModeAsync(SettlementProcessingMode.Automatic, Guid.NewGuid());

        await orchestrator.TriggerAsync(payout.Id);

        gateway.Verify(
            item => item.CreatePayoutAsync(It.IsAny<CreatePayoutCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        var reservation = await context.PayoutExecutionReservations.SingleAsync(item => item.PayoutId == payout.Id);
        reservation.Mode.Should().Be(PayoutExecutionMode.Manual);
        reservation.Status.Should().Be(PayoutExecutionReservationStatus.Claimed);
    }

    [Fact]
    public async Task Manual_submission_requires_claim_and_dual_control_confirmation_by_default()
    {
        await using var context = TestDbContextFactory.Create();
        var owner = CreateVendorForToday();
        var settlement = CreateApprovedSettlement(owner);
        var payout = new Payout(settlement.Id, 100m);
        context.Vendors.Add(owner);
        context.Settlements.Add(settlement);
        context.Payouts.Add(payout);
        await context.SaveChangesAsync();

        var settings = new SettlementProcessingSettingsService(context);
        var submittingAdmin = Guid.NewGuid();
        var confirmingAdmin = Guid.NewGuid();
        await settings.UpdateAsync(
            SettlementProcessingMode.Manual,
            [(PayoutScheduleDay)SaudiTime.Today.DayOfWeek],
            submittingAdmin);

        var orchestrator = CreateOrchestrator(context, [], settings);

        await orchestrator.ClaimManualAsync(payout.Id, submittingAdmin);
        await orchestrator.RecordManualBankSubmissionAsync(payout.Id, "BANK-SUBMISSION-001", submittingAdmin);
        var confirmationProof = CreateProofAttachment(payout.Id, PayoutProofKind.ManualTransfer, confirmingAdmin);
        context.PayoutProofAttachments.Add(confirmationProof);
        await context.SaveChangesAsync();

        var sameActor = () => orchestrator.ConfirmManualAsync(
            payout.Id,
            "BANK-FINAL-001",
            confirmationProof.Id,
            submittingAdmin);
        await sameActor.Should().ThrowAsync<BusinessRuleException>()
            .Where(error => error.ErrorCode == "PAYOUT_DUAL_CONTROL_REQUIRED");

        await orchestrator.ConfirmManualAsync(
            payout.Id,
            "BANK-FINAL-001",
            confirmationProof.Id,
            confirmingAdmin);

        var stored = await context.Payouts
            .Include(item => item.ExecutionReservation)
            .Include(item => item.ManualConfirmation)
            .SingleAsync(item => item.Id == payout.Id);
        stored.Status.Should().Be(PayoutStatus.Paid);
        stored.ExecutionReservation!.Status.Should().Be(PayoutExecutionReservationStatus.Confirmed);
        stored.ManualConfirmation!.ConfirmedByUserId.Should().Be(confirmingAdmin);
    }

    [Fact]
    public async Task Manual_confirmation_with_a_long_bank_reference_uses_a_bounded_financial_event_idempotency_key()
    {
        await using var context = TestDbContextFactory.Create();
        var owner = CreateVendorForToday();
        var settlement = CreateApprovedSettlement(owner);
        var payout = new Payout(settlement.Id, 100m);
        context.Vendors.Add(owner);
        context.Settlements.Add(settlement);
        context.Payouts.Add(payout);
        await context.SaveChangesAsync();

        var settings = new SettlementProcessingSettingsService(context);
        var submittingAdmin = Guid.NewGuid();
        var confirmingAdmin = Guid.NewGuid();
        await settings.UpdateAsync(
            SettlementProcessingMode.Manual,
            [(PayoutScheduleDay)SaudiTime.Today.DayOfWeek],
            submittingAdmin);

        var orchestrator = CreateOrchestrator(context, [], settings);
        await orchestrator.ClaimManualAsync(payout.Id, submittingAdmin);
        await orchestrator.RecordManualBankSubmissionAsync(payout.Id, "BANK-SUBMISSION-001", submittingAdmin);
        var proof = CreateProofAttachment(payout.Id, PayoutProofKind.ManualTransfer, confirmingAdmin);
        context.PayoutProofAttachments.Add(proof);
        await context.SaveChangesAsync();

        await orchestrator.ConfirmManualAsync(
            payout.Id,
            new string('R', 200),
            proof.Id,
            confirmingAdmin);

        var postedEvent = await context.FinancialEvents.SingleAsync(item => item.PayoutId == payout.Id);
        postedEvent.IdempotencyKey.Length.Should().BeLessThanOrEqualTo(160);
        postedEvent.IdempotencyKey.Should().StartWith($"payout-paid:{payout.Id:N}:sha256:");
    }

    [Fact]
    public async Task Cancellation_releases_vendor_hold_only_before_manual_bank_submission()
    {
        await using var context = TestDbContextFactory.Create();
        var owner = CreateVendorForToday();
        var settlement = CreateApprovedSettlement(owner);
        var payout = new Payout(settlement.Id, 100m);
        context.Vendors.Add(owner);
        context.Settlements.Add(settlement);
        context.Payouts.Add(payout);
        await context.SaveChangesAsync();

        var settings = new SettlementProcessingSettingsService(context);
        var admin = Guid.NewGuid();
        await settings.UpdateAsync(
            SettlementProcessingMode.Manual,
            [(PayoutScheduleDay)SaudiTime.Today.DayOfWeek],
            admin);

        var vendorWalletService = new VendorPayoutWalletService(
            context,
            NullLogger<VendorPayoutWalletService>.Instance);
        await vendorWalletService.EnsureHoldAsync(
            owner.Id,
            settlement.Id,
            payout.Amount,
            "test",
            "Test payout hold",
            CancellationToken.None);
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context, [], settings, vendorWalletService);
        await orchestrator.ClaimManualAsync(payout.Id, admin);
        await orchestrator.CancelAsync(payout.Id);

        var storedPayout = await context.Payouts.SingleAsync(item => item.Id == payout.Id);
        storedPayout.Status.Should().Be(PayoutStatus.Cancelled);
        var hold = await context.WalletHolds.SingleAsync(item => item.ReferenceId == settlement.Id);
        hold.Status.Should().Be(WalletHoldStatus.Released);

        // A second payout proves that cancellation is explicitly blocked once
        // an external bank submission has been recorded.
        var secondSettlement = CreateApprovedSettlement(owner);
        var secondPayout = new Payout(secondSettlement.Id, 75m);
        context.Settlements.Add(secondSettlement);
        context.Payouts.Add(secondPayout);
        await context.SaveChangesAsync();
        await orchestrator.ClaimManualAsync(secondPayout.Id, admin);
        await orchestrator.RecordManualBankSubmissionAsync(secondPayout.Id, "BANK-SUBMISSION-002", admin);

        var cancelSubmitted = () => orchestrator.CancelAsync(secondPayout.Id);
        await cancelSubmitted.Should().ThrowAsync<BusinessRuleException>()
            .Where(error => error.ErrorCode == "PAYOUT_RECONCILIATION_REQUIRED");
    }

    [Fact]
    public async Task Returned_paid_payout_creates_immutable_reversal_and_opposite_financial_posting()
    {
        await using var context = TestDbContextFactory.Create();
        var owner = CreateVendorForToday();
        var settlement = CreateApprovedSettlement(owner);
        var payout = new Payout(settlement.Id, 100m);
        payout.MarkAsPaid("BANK-PAID-001", providerName: "Manual");
        settlement.MarkPaidOut();
        context.Vendors.Add(owner);
        context.Settlements.Add(settlement);
        context.Payouts.Add(payout);
        await context.SaveChangesAsync();

        var settings = new SettlementProcessingSettingsService(context);
        var orchestrator = CreateOrchestrator(context, [], settings);
        var approver = Guid.NewGuid();
        var returnProof = CreateProofAttachment(payout.Id, PayoutProofKind.ReturnedFunds, approver);
        context.PayoutProofAttachments.Add(returnProof);
        await context.SaveChangesAsync();

        await orchestrator.RecordReturnAsync(
            payout.Id,
            "BANK-RETURN-001",
            returnProof.Id,
            approver,
            "Beneficiary account rejected the transfer.");

        var stored = await context.Payouts
            .Include(item => item.Reversal)
            .Include(item => item.Settlement)
            .SingleAsync(item => item.Id == payout.Id);
        stored.Status.Should().Be(PayoutStatus.Reversed);
        stored.Settlement.Status.Should().Be(SettlementStatus.Reversed);
        stored.Reversal!.ReturnReference.Should().Be("BANK-RETURN-001");
        context.FinancialEvents.Should().ContainSingle(item =>
            item.PayoutId == payout.Id &&
            item.EventType == Zadana.Domain.Modules.Finances.Enums.FinancialEventType.VendorPayoutReversed);
    }

    private static PayoutOrchestrator CreateOrchestrator(
        IApplicationDbContext context,
        IEnumerable<IPayoutGateway> gateways,
        ISettlementProcessingSettingsService settings,
        VendorPayoutWalletService? vendorWalletService = null)
    {
        var payoutWalletService = vendorWalletService ?? new VendorPayoutWalletService(
            context,
            NullLogger<VendorPayoutWalletService>.Instance);
        return new PayoutOrchestrator(
            context,
            gateways,
            new FinancialEventPostingService(context, NullLogger<FinancialEventPostingService>.Instance),
            new WalletProjectionUpdater(context),
            payoutWalletService,
            Options.Create(new FinancialSettingsOptions()),
            new NoOpAdminAlertService(),
            Mock.Of<INotificationService>(),
            Mock.Of<IOneSignalPushService>(),
            settings);
    }

    private static PayoutProofAttachment CreateProofAttachment(
        Guid payoutId,
        PayoutProofKind kind,
        Guid uploadedByUserId) =>
        new(
            payoutId,
            kind,
            "proof.pdf",
            "application/pdf",
            1,
            new string('A', 64),
            [0x01],
            uploadedByUserId);

    private static Vendor CreateVendorForToday()
    {
        var vendor = new Vendor(
            Guid.NewGuid(),
            "تاجر اختبار",
            "Payout execution vendor",
            "Retail",
            $"CR-{Guid.NewGuid():N}"[..12],
            $"payout-{Guid.NewGuid():N}@example.test",
            "0500000000");
        vendor.UpdatePayoutDay((PayoutScheduleDay)SaudiTime.Today.DayOfWeek);
        return vendor;
    }

    private static Settlement CreateApprovedSettlement(Vendor owner)
    {
        var settlement = new Settlement(owner.Id, null);
        settlement.UpdateTotals(100m, 0m);
        settlement.Approve();
        return settlement;
    }

    private sealed class NoOpAdminAlertService : IAdminAlertService
    {
        public Task<AdminAlertDispatchResult> SendAsync(
            AdminAlertRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdminAlertDispatchResult(
                0,
                0,
                new OneSignalPushDispatchResult(false, false, true, null, null, "test")));
    }
}
