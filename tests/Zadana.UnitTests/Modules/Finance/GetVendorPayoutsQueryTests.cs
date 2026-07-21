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
        var confirmation = new PayoutManualConfirmation(
            payout.Id,
            "BANK-REF-123",
            "https://files.zadna0.com/payout-proofs/BANK-REF-123.pdf",
            confirmedByUserId);

        context.Settlements.Add(settlement);
        context.Payouts.Add(payout);
        context.PayoutManualConfirmations.Add(confirmation);
        await context.SaveChangesAsync();

        var handler = new GetVendorPayoutsQueryHandler(context);

        var result = await handler.Handle(new GetVendorPayoutsQuery(vendorId), CancellationToken.None);

        var dto = result.Items.Should().ContainSingle().Subject;
        dto.SettlementStatus.Should().Be(nameof(SettlementStatus.Approved));
        dto.ManualConfirmation.Should().NotBeNull();
        dto.ManualConfirmation!.TransferReference.Should().Be("BANK-REF-123");
        dto.ManualConfirmation.ProofUrl.Should().Be("https://files.zadna0.com/payout-proofs/BANK-REF-123.pdf");
        dto.ManualConfirmation.ConfirmedByUserId.Should().Be(confirmedByUserId);
        dto.ManualConfirmation.ConfirmedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }
}
