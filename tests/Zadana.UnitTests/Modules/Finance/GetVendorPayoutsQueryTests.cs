using FluentAssertions;
using Zadana.Application.Modules.Wallets.Queries.GetVendorPayouts;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Finance;

public sealed class GetVendorPayoutsQueryTests
{
    [Fact]
    public async Task Handle_includes_settlement_status_and_manual_confirmation_audit_data()
    {
        await using var context = TestDbContextFactory.Create();
        var vendorId = Guid.NewGuid();
        var confirmedByUserId = Guid.NewGuid();
        var settlement = new Settlement(vendorId, null, SettlementOrigin.ScheduledCycle);
        settlement.UpdateTotals(250m, 25m);
        settlement.Approve();

        var payout = new Payout(settlement.Id, settlement.NetAmount);
        var proofAttachment = new PayoutProofAttachment(
            payout.Id,
            PayoutProofKind.ManualTransfer,
            "proof.pdf",
            "application/pdf",
            1,
            new string('A', 64),
            [0x01],
            confirmedByUserId);
        var confirmation = new PayoutManualConfirmation(
            payout.Id,
            "BANK-REF-123",
            proofAttachment.Id,
            confirmedByUserId);

        context.Settlements.Add(settlement);
        context.Payouts.Add(payout);
        context.PayoutProofAttachments.Add(proofAttachment);
        context.PayoutManualConfirmations.Add(confirmation);
        await context.SaveChangesAsync();

        var handler = new GetVendorPayoutsQueryHandler(context);

        var result = await handler.Handle(new GetVendorPayoutsQuery(vendorId), CancellationToken.None);

        var dto = result.Items.Should().ContainSingle().Subject;
        dto.SettlementStatus.Should().Be(nameof(SettlementStatus.Approved));
        dto.ManualConfirmation.Should().NotBeNull();
        dto.ManualConfirmation!.TransferReference.Should().Be("BANK-REF-123");
        dto.ManualConfirmation.ProofAttachmentId.Should().Be(proofAttachment.Id);
        dto.ManualConfirmation.HasLegacyProof.Should().BeFalse();
        dto.ManualConfirmation.ConfirmedByUserId.Should().Be(confirmedByUserId);
        dto.ManualConfirmation.ConfirmedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }
}
