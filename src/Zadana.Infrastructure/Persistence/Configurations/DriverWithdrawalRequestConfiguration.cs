using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DriverWithdrawalRequestConfiguration : IEntityTypeConfiguration<DriverWithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<DriverWithdrawalRequest> builder)
    {
        builder.ToTable("DriverWithdrawalRequests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(DriverWithdrawalStatus.Pending)
            .IsRequired();

        builder.Property(x => x.TransferReference)
            .HasMaxLength(200);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(500);

        builder.HasOne(x => x.Wallet)
            .WithMany()
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DriverPayoutMethod)
            .WithMany()
            .HasForeignKey(x => x.DriverPayoutMethodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.DriverId);
        builder.HasIndex(x => x.WalletId);
    }
}
