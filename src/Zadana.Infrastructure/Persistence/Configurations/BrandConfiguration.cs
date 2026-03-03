using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brand");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.LogoUrl)
            .HasMaxLength(500);

        builder.Property(b => b.IsActive)
            .IsRequired()
            .HasDefaultValue(true);
    }
}
