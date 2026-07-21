using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Modules.Finances.Services;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.Infrastructure.Persistence;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Finance;

public sealed class PayoutProofAttachmentServiceTests
{
    [Fact]
    public async Task Manual_proof_accepts_recoverable_failed_payout_with_submitted_manual_reservation()
    {
        await using var context = TestDbContextFactory.Create();
        var payout = await CreateSubmittedManualPayoutAsync(context);
        payout.MarkAsFailed("Legacy state drift", providerName: "Manual");
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var attachment = await service.UploadAsync(
            payout.Id,
            PayoutProofKind.ManualTransfer,
            CreatePdf("proof-a"),
            Guid.NewGuid());

        attachment.PayoutId.Should().Be(payout.Id);
        context.PayoutProofAttachments.Should().ContainSingle();
    }

    [Fact]
    public async Task Identical_proof_retry_returns_existing_attachment_after_payout_is_closed()
    {
        await using var context = TestDbContextFactory.Create();
        var payout = await CreateSubmittedManualPayoutAsync(context);
        var service = CreateService(context);
        var uploadedBy = Guid.NewGuid();

        var first = await service.UploadAsync(
            payout.Id,
            PayoutProofKind.ManualTransfer,
            CreatePdf("same-proof"),
            uploadedBy);

        payout.MarkAsManuallyPaid("BANK-FINAL-001", uploadedBy);
        await context.SaveChangesAsync();

        var retry = await service.UploadAsync(
            payout.Id,
            PayoutProofKind.ManualTransfer,
            CreatePdf("same-proof"),
            uploadedBy);

        retry.Id.Should().Be(first.Id);
        context.PayoutProofAttachments.Should().ContainSingle();
    }

    [Fact]
    public async Task Different_proof_is_rejected_after_payout_is_closed()
    {
        await using var context = TestDbContextFactory.Create();
        var payout = await CreateSubmittedManualPayoutAsync(context);
        var service = CreateService(context);
        var uploadedBy = Guid.NewGuid();

        await service.UploadAsync(
            payout.Id,
            PayoutProofKind.ManualTransfer,
            CreatePdf("original-proof"),
            uploadedBy);
        payout.MarkAsManuallyPaid("BANK-FINAL-001", uploadedBy);
        await context.SaveChangesAsync();

        var action = () => service.UploadAsync(
            payout.Id,
            PayoutProofKind.ManualTransfer,
            CreatePdf("different-proof"),
            uploadedBy);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(error => error.ErrorCode == "PAYOUT_INVALID_STATUS");
    }

    private static PayoutProofAttachmentService CreateService(
        ApplicationDbContext context) =>
        new(context, new EphemeralDataProtectionProvider());

    private static async Task<Payout> CreateSubmittedManualPayoutAsync(
        ApplicationDbContext context)
    {
        var vendor = new Vendor(
            Guid.NewGuid(),
            "تاجر اختبار",
            "Proof test vendor",
            "Retail",
            $"CR-{Guid.NewGuid():N}"[..12],
            $"proof-{Guid.NewGuid():N}@example.test",
            "0500000000");
        var settlement = new Settlement(vendor.Id, null);
        settlement.UpdateTotals(100m, 0m);
        settlement.Approve();
        var payout = new Payout(settlement.Id, 100m);
        var adminId = Guid.NewGuid();
        var reservation = new PayoutExecutionReservation(
            payout.Id,
            PayoutExecutionMode.Manual,
            adminId);
        reservation.MarkSubmitted(adminId, "BANK-SUBMISSION-001");
        payout.MarkAsProcessing(providerName: "Manual");
        settlement.MarkAsProcessing();

        context.Vendors.Add(vendor);
        context.Settlements.Add(settlement);
        context.Payouts.Add(payout);
        context.PayoutExecutionReservations.Add(reservation);
        await context.SaveChangesAsync();
        return payout;
    }

    private static FormFile CreatePdf(string marker)
    {
        var content = System.Text.Encoding.UTF8.GetBytes($"%PDF-1.4\n{marker}\n%%EOF");
        return new FormFile(new MemoryStream(content), 0, content.Length, "file", "proof.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }
}
