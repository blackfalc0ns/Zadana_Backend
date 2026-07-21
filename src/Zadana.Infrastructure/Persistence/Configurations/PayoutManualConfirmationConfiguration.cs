using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class PayoutManualConfirmationConfiguration : IEntityTypeConfiguration<PayoutManualConfirmation>
{
    public void Configure(EntityTypeBuilder<PayoutManualConfirmation> builder)
    {
        builder.ToTable("PayoutManualConfirmations");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.TransferReference)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(item => item.ProofUrl)
            .IsRequired()
            .HasMaxLength(2000);

        builder.HasIndex(item => item.PayoutId)
            .IsUnique()
            .HasDatabaseName("IX_PayoutManualConfirmations_PayoutId");

        builder.HasIndex(item => new { item.ConfirmedByUserId, item.ConfirmedAtUtc })
            .HasDatabaseName("IX_PayoutManualConfirmations_ConfirmedBy_ConfirmedAt");
    }
}
