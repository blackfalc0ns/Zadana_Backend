using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class PayoutBankStatementEntryConfiguration : IEntityTypeConfiguration<PayoutBankStatementEntry>
{
    public void Configure(EntityTypeBuilder<PayoutBankStatementEntry> builder)
    {
        builder.ToTable("PayoutBankStatementEntries");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.BankReference).HasMaxLength(200).IsRequired();
        builder.Property(item => item.NormalizedBankReference).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(item => item.TransactionDateUtc).IsRequired();
        builder.Property(item => item.CurrencyCode).HasMaxLength(8).IsRequired();
        builder.Property(item => item.BeneficiaryMasked).HasMaxLength(256);
        builder.Property(item => item.Memo).HasMaxLength(500);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.ResolutionNote).HasMaxLength(1000);

        builder.HasIndex(item => new { item.ImportId, item.RowNumber })
            .IsUnique()
            .HasDatabaseName("UX_PayoutBankStatementEntries_Import_Row");
        builder.HasIndex(item => new { item.Status, item.TransactionDateUtc })
            .HasDatabaseName("IX_PayoutBankStatementEntries_Status_Date");
        builder.HasIndex(item => new { item.NormalizedBankReference, item.Amount })
            .HasDatabaseName("IX_PayoutBankStatementEntries_Reference_Amount");
        builder.HasIndex(item => item.PayoutId)
            .IsUnique()
            .HasFilter("[PayoutId] IS NOT NULL")
            .HasDatabaseName("UX_PayoutBankStatementEntries_PayoutId");

        builder.HasOne(item => item.Payout)
            .WithMany()
            .HasForeignKey(item => item.PayoutId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
