using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class PayoutReversalConfiguration : IEntityTypeConfiguration<PayoutReversal>
{
    public void Configure(EntityTypeBuilder<PayoutReversal> builder)
    {
        builder.ToTable("PayoutReversals");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.ReturnReference)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(item => item.LegacyProofUrl)
            .HasColumnName("ProofUrl")
            .HasMaxLength(2000);

        builder.Property(item => item.ProofAttachmentId);

        builder.Property(item => item.Reason)
            .HasMaxLength(1000);

        builder.HasIndex(item => item.PayoutId)
            .IsUnique()
            .HasDatabaseName("IX_PayoutReversals_PayoutId");

        builder.HasIndex(item => new { item.ConfirmedByUserId, item.ConfirmedAtUtc })
            .HasDatabaseName("IX_PayoutReversals_ConfirmedBy_ConfirmedAt");

        builder.HasIndex(item => item.ProofAttachmentId)
            .HasDatabaseName("IX_PayoutReversals_ProofAttachmentId");

        builder.HasOne(item => item.ProofAttachment)
            .WithMany()
            .HasForeignKey(item => item.ProofAttachmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
