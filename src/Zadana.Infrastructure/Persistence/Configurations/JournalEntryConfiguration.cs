using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Finances.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SequenceNumber)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired()
            .HasDefaultValue("SAR");

        builder.Property(x => x.Memo)
            .HasMaxLength(500);

        builder.HasIndex(x => x.SequenceNumber)
            .IsUnique()
            .HasDatabaseName("IX_JournalEntries_SequenceNumber");

        builder.HasIndex(x => x.FinancialEventId)
            .IsUnique()
            .HasDatabaseName("IX_JournalEntries_FinancialEventId");

        builder.HasOne(x => x.FinancialEvent)
            .WithOne(x => x.JournalEntry)
            .HasForeignKey<JournalEntry>(x => x.FinancialEventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(JournalEntry.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
