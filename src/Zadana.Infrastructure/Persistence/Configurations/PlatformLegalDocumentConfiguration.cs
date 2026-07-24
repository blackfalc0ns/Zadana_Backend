using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Marketing.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class PlatformLegalDocumentConfiguration : IEntityTypeConfiguration<PlatformLegalDocument>
{
    public void Configure(EntityTypeBuilder<PlatformLegalDocument> builder)
    {
        builder.ToTable("PlatformLegalDocuments");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(item => item.ContentAr).IsRequired();
        builder.Property(item => item.ContentEn).IsRequired();
        builder.Property(item => item.Version).HasMaxLength(32).IsRequired();
        builder.Property(item => item.EffectiveAtUtc).IsRequired();

        builder.HasIndex(item => item.DocumentType)
            .IsUnique()
            .HasDatabaseName("IX_PlatformLegalDocuments_DocumentType");
    }
}
