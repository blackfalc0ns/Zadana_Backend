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

        builder.Property(x => x.RequestIdempotencyKey)
            .HasMaxLength(160);

        builder.Property(x => x.RequestedPayoutDay)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.DestinationSnapshot)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(DriverWithdrawalStatus.Pending)
            // Processing a withdrawal creates a payout with a client-generated
            // id.  Treat both state and that link as optimistic concurrency
            // tokens so two administrators cannot attach two payouts to the
            // same withdrawal.
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(x => x.PayoutId)
            .IsConcurrencyToken();

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

        builder.HasOne(x => x.Payout)
            .WithMany()
            .HasForeignKey(x => x.PayoutId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.DriverId);
        builder.HasIndex(x => new { x.DriverId, x.Status })
            .HasDatabaseName("IX_DriverWithdrawalRequests_Driver_Status");
        builder.HasIndex(x => x.DriverId)
            .IsUnique()
            .HasFilter("[Status] IN ('Pending', 'Processing')")
            .HasDatabaseName("UX_DriverWithdrawalRequests_OneActivePerDriver");
        builder.HasIndex(x => new { x.DriverId, x.RequestIdempotencyKey })
            .IsUnique()
            .HasFilter("[RequestIdempotencyKey] IS NOT NULL")
            .HasDatabaseName("UX_DriverWithdrawalRequests_Driver_IdempotencyKey");
        builder.HasIndex(x => x.WalletId);
        // A payout belongs to a single withdrawal. The withdrawal's
        // PayoutId concurrency token protects two payouts being attached to
        // one withdrawal; this filtered unique index protects the inverse
        // link from accidental reuse as well.
        builder.HasIndex(x => x.PayoutId)
            .IsUnique()
            .HasFilter("[PayoutId] IS NOT NULL")
            .HasDatabaseName("UX_DriverWithdrawalRequests_PayoutId");
    }
}
