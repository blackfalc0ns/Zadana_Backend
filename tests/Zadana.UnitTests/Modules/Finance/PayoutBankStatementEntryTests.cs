using FluentAssertions;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Finance;

public sealed class PayoutBankStatementEntryTests
{
    [Fact]
    public void Matched_statement_entry_cannot_be_silently_reassigned_or_ignored()
    {
        var entry = CreateEntry();
        entry.Match(Guid.NewGuid(), Guid.NewGuid(), "Matched during bank reconciliation.");

        var reassignment = () => entry.Match(Guid.NewGuid(), Guid.NewGuid(), "Attempted reassignment.");
        var ignore = () => entry.MarkIgnored(Guid.NewGuid(), "Attempted overwrite.");

        reassignment.Should().Throw<BusinessRuleException>()
            .Where(error => error.ErrorCode == "BANK_STATEMENT_ENTRY_ALREADY_RESOLVED");
        ignore.Should().Throw<BusinessRuleException>()
            .Where(error => error.ErrorCode == "BANK_STATEMENT_ENTRY_ALREADY_RESOLVED");
    }

    [Fact]
    public void Ignored_statement_entry_cannot_be_later_matched()
    {
        var entry = CreateEntry();
        entry.MarkIgnored(Guid.NewGuid(), "Not an outbound payout.");

        var match = () => entry.Match(Guid.NewGuid(), Guid.NewGuid(), "Attempted late match.");

        match.Should().Throw<BusinessRuleException>()
            .Where(error => error.ErrorCode == "BANK_STATEMENT_ENTRY_ALREADY_RESOLVED");
    }

    private static PayoutBankStatementEntry CreateEntry() =>
        new(
            Guid.NewGuid(),
            rowNumber: 2,
            bankReference: "BANK-REF-001",
            normalizedBankReference: "BANKREF001",
            amount: 125.50m,
            transactionDateUtc: DateTime.UtcNow,
            beneficiaryMasked: "****1234",
            currencyCode: "SAR");
}
