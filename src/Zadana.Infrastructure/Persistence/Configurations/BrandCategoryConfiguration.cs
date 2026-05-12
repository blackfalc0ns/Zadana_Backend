using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class BrandCategoryConfiguration : IEntityTypeConfiguration<BrandCategory>
{
    public void Configure(EntityTypeBuilder<BrandCategory> builder)
    {
        builder.ToTable("BrandCategories");

        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Brand)
            .WithMany(x => x.BrandCategories)
            .HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Category)
            .WithMany(x => x.BrandCategories)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.BrandId, x.CategoryId })
            .IsUnique();

        builder.HasIndex(x => x.CategoryId);
    }
}
