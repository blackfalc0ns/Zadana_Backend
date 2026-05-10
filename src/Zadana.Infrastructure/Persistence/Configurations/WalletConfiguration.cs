using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallet");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.OwnerType)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(w => w.OwnerId)
            .IsRequired();

        builder.Property(w => w.CurrentBalance)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(w => w.PendingBalance)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(w => w.CodOwedBalance)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(w => w.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired()
            .HasDefaultValue("EGP");

        builder.Property(w => w.LastJournalSequence)
            .IsRequired()
            .HasDefaultValue(0L);

        // Indexes
        builder.HasIndex(w => new { w.OwnerType, w.OwnerId })
            .IsUnique()
            .HasDatabaseName("IX_Wallet_Owner");
    }
}
