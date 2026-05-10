using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Finances.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.ToTable("JournalLines");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AccountCode)
            .HasConversion<string>()
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.OwnerType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.DebitAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.CreditAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired()
            .HasDefaultValue("EGP");

        builder.Property(x => x.Memo)
            .HasMaxLength(500);

        builder.HasOne(x => x.JournalEntry)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.AccountCode, x.OwnerType, x.OwnerId })
            .HasDatabaseName("IX_JournalLines_AccountOwner");

        builder.HasIndex(x => x.OrderId)
            .HasDatabaseName("IX_JournalLines_OrderId");
    }
}
