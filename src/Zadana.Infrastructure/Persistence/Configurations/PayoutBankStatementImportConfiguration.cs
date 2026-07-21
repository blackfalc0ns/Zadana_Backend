using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class PayoutBankStatementImportConfiguration : IEntityTypeConfiguration<PayoutBankStatementImport>
{
    public void Configure(EntityTypeBuilder<PayoutBankStatementImport> builder)
    {
        builder.ToTable("PayoutBankStatementImports");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.FileName).HasMaxLength(255).IsRequired();
        builder.Property(item => item.FileSha256).HasMaxLength(64).IsRequired();
        builder.Property(item => item.ImportedByUserId).IsRequired();
        builder.Property(item => item.ImportedAtUtc).IsRequired();

        builder.HasIndex(item => item.FileSha256)
            .IsUnique()
            .HasDatabaseName("UX_PayoutBankStatementImports_FileSha256");
        builder.HasIndex(item => new { item.ImportedAtUtc, item.ImportedByUserId })
            .HasDatabaseName("IX_PayoutBankStatementImports_ImportedAt_ImportedBy");

        builder.HasMany(item => item.Entries)
            .WithOne(item => item.Import)
            .HasForeignKey(item => item.ImportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
