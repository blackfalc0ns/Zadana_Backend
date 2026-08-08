using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class PayoutProofAttachmentConfiguration : IEntityTypeConfiguration<PayoutProofAttachment>
{
    public void Configure(EntityTypeBuilder<PayoutProofAttachment> builder)
    {
        builder.ToTable("PayoutProofAttachments");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(item => item.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(item => item.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(item => item.ContentLength)
            .IsRequired();

        builder.Property(item => item.Sha256)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(item => item.ProtectedContent)
            .IsRequired();

        builder.Property(item => item.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(item => new { item.PayoutId, item.Kind, item.Sha256 })
            .IsUnique()
            .HasDatabaseName("UX_PayoutProofAttachments_PayoutId_Kind_Sha256");

        builder.HasIndex(item => new { item.PayoutId, item.Kind, item.FinalizedAtUtc })
            .HasDatabaseName("IX_PayoutProofAttachments_PayoutId_Kind_FinalizedAt");
    }
}
