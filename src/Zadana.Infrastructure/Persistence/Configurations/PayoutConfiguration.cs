using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.ToTable("Payouts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.DestinationType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.DestinationSnapshot).HasMaxLength(2000);
        builder.Property(x => x.ProviderName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ProviderTransferId).HasMaxLength(200);
        builder.Property(x => x.ProviderSequenceNumber).HasMaxLength(32);
        builder.Property(x => x.TransferReference).HasMaxLength(200);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);

        builder.HasIndex(x => x.ProviderTransferId)
            .IsUnique()
            .HasFilter("[ProviderTransferId] IS NOT NULL")
            .HasDatabaseName("IX_Payouts_ProviderTransferId");

        builder.HasOne(x => x.VendorBankAccount)
            .WithMany()
            .HasForeignKey(x => x.VendorBankAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Attempts)
            .WithOne(x => x.Payout)
            .HasForeignKey(x => x.PayoutId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ManualConfirmation)
            .WithOne(x => x.Payout)
            .HasForeignKey<PayoutManualConfirmation>(x => x.PayoutId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
