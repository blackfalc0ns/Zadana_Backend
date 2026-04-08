using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class FeaturedProductPlacementConfiguration : IEntityTypeConfiguration<FeaturedProductPlacement>
{
    public void Configure(EntityTypeBuilder<FeaturedProductPlacement> builder)
    {
        builder.ToTable("FeaturedProductPlacement");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PlacementType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.Property(x => x.Note)
            .HasMaxLength(500);

        builder.HasOne(x => x.VendorProduct)
            .WithMany()
            .HasForeignKey(x => x.VendorProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MasterProduct)
            .WithMany()
            .HasForeignKey(x => x.MasterProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.IsActive, x.DisplayOrder })
            .HasDatabaseName("IX_FeaturedProductPlacement_IsActive_DisplayOrder");

        builder.HasIndex(x => new { x.PlacementType, x.VendorProductId, x.MasterProductId })
            .HasDatabaseName("IX_FeaturedProductPlacement_Target");
    }
}
