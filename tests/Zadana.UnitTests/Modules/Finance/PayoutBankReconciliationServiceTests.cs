using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Finance;

public sealed class PayoutBankReconciliationServiceTests
{
    [Fact]
    public async Task Import_accepts_utf8_arabic_headers_and_arabic_indic_amounts()
    {
        await using var context = TestDbContextFactory.Create();
        var service = new PayoutBankReconciliationService(context);
        var csv = string.Join('\n',
        [
            // المرجع,المبلغ,تاريخ التحويل,المستفيد
            "\u0627\u0644\u0645\u0631\u062c\u0639,\u0627\u0644\u0645\u0628\u0644\u063a,\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u062a\u062d\u0648\u064a\u0644,\u0627\u0644\u0645\u0633\u062a\u0641\u064a\u062f",
            // BANK-REF-001,١٢٥.٥٠ ر.س,2026-07-20,حساب تجريبي
            "BANK-REF-001,\u0661\u0662\u0665.\u0665\u0660 \u0631.\u0633,2026-07-20,\u062d\u0633\u0627\u0628 \u062a\u062c\u0631\u064a\u0628\u064a"
        ]);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportAsync(stream, "outbound-payouts.csv", Guid.NewGuid());

        result.TotalRows.Should().Be(1);
        result.UnmatchedRows.Should().Be(1);
        result.InvalidRows.Should().Be(0);

        var entry = await context.PayoutBankStatementEntries.SingleAsync();
        entry.BankReference.Should().Be("BANK-REF-001");
        entry.Amount.Should().Be(125.50m);
        entry.CurrencyCode.Should().Be("SAR");
        entry.BeneficiaryMasked.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Match_is_idempotent_for_the_same_payout_but_cannot_reassign_a_resolved_row()
    {
        await using var context = TestDbContextFactory.Create();
        var service = new PayoutBankReconciliationService(context);
        var actorUserId = Guid.NewGuid();
        var import = new PayoutBankStatementImport("outbound-payouts.csv", new string('A', 64), actorUserId);
        var firstSettlement = new Settlement(Guid.NewGuid(), null);
        var firstPayout = new Payout(firstSettlement.Id, 125.50m);
        firstPayout.MarkAsPaid("BANK-REF-001", providerName: "Manual");
        var entry = new PayoutBankStatementEntry(
            import.Id,
            rowNumber: 2,
            bankReference: "BANK-REF-001",
            normalizedBankReference: "BANKREF001",
            amount: 125.50m,
            transactionDateUtc: DateTime.UtcNow);

        context.AddRange(import, firstSettlement, firstPayout, entry);
        await context.SaveChangesAsync();

        var firstMatch = await service.MatchAsync(entry.Id, firstPayout.Id, actorUserId);
        var retryMatch = await service.MatchAsync(entry.Id, firstPayout.Id, actorUserId);

        firstMatch.PayoutId.Should().Be(firstPayout.Id);
        retryMatch.PayoutId.Should().Be(firstPayout.Id);

        var secondSettlement = new Settlement(Guid.NewGuid(), null);
        var secondPayout = new Payout(secondSettlement.Id, 125.50m);
        secondPayout.MarkAsPaid("BANK-REF-001", providerName: "Manual");
        context.AddRange(secondSettlement, secondPayout);
        await context.SaveChangesAsync();

        var reassignment = () => service.MatchAsync(entry.Id, secondPayout.Id, actorUserId);
        await reassignment.Should().ThrowAsync<BusinessRuleException>()
            .Where(error => error.ErrorCode == "BANK_STATEMENT_ENTRY_ALREADY_RESOLVED");
    }
}
