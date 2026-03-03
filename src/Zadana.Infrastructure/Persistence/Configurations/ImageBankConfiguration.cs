using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class ImageBankConfiguration : IEntityTypeConfiguration<ImageBank>
{
    public void Configure(EntityTypeBuilder<ImageBank> builder)
    {
        builder.ToTable("ImageBank");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Url)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(i => i.AltText)
            .HasMaxLength(200);

        builder.Property(i => i.Tags)
            .HasMaxLength(500);
    }
}
