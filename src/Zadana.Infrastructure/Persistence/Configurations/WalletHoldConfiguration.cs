using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.SharedKernel.Finance;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class WalletHoldConfiguration : IEntityTypeConfiguration<WalletHold>
{
    public void Configure(EntityTypeBuilder<WalletHold> builder)
    {
        builder.ToTable("WalletHolds");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OwnerType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired()
            .HasDefaultValue(CurrencyPolicy.OfficialCurrency);

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.ReferenceType).HasMaxLength(80);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.Memo).HasMaxLength(500);

        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_WalletHolds_IdempotencyKey");

        builder.HasIndex(x => new { x.OwnerType, x.OwnerId, x.Status })
            .HasDatabaseName("IX_WalletHolds_Owner_Status");

        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId })
            .HasDatabaseName("IX_WalletHolds_Reference");
    }
}
