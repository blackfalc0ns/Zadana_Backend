using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class PayoutAttemptConfiguration : IEntityTypeConfiguration<PayoutAttempt>
{
    public void Configure(EntityTypeBuilder<PayoutAttempt> builder)
    {
        builder.ToTable("PayoutAttempts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AttemptType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.ProviderName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ProviderTransferId).HasMaxLength(200);
        builder.Property(x => x.TransferReference).HasMaxLength(200);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.RawPayload).HasMaxLength(4000);

        builder.HasIndex(x => new { x.PayoutId, x.AttemptType, x.ProviderTransferId })
            .HasDatabaseName("IX_PayoutAttempts_Payout_Attempt_ProviderTransfer");
    }
}
