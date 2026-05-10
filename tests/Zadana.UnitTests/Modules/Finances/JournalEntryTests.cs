using FluentAssertions;
using Zadana.Domain.Modules.Finances.Entities;
using Zadana.Domain.Modules.Finances.Enums;

namespace Zadana.UnitTests.Modules.Finances;

public class JournalEntryTests
{
    [Fact]
    public void EnsureBalanced_WhenDebitsEqualCredits_DoesNotThrow()
    {
        var entry = new JournalEntry(Guid.NewGuid(), sequenceNumber: 1);

        entry.AddLine(new JournalLine(entry.Id, FinancialAccountCode.PlatformCash, 100m, 0m));
        entry.AddLine(new JournalLine(entry.Id, FinancialAccountCode.PlatformRevenue, 0m, 100m));

        var act = () => entry.EnsureBalanced();

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureBalanced_WhenDebitsDoNotEqualCredits_Throws()
    {
        var entry = new JournalEntry(Guid.NewGuid(), sequenceNumber: 1);

        entry.AddLine(new JournalLine(entry.Id, FinancialAccountCode.PlatformCash, 100m, 0m));
        entry.AddLine(new JournalLine(entry.Id, FinancialAccountCode.PlatformRevenue, 0m, 99m));

        var act = () => entry.EnsureBalanced();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Journal entry must balance debits and credits.");
    }
}
