using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class VendorProductConfiguration : IEntityTypeConfiguration<VendorProduct>
{
    public void Configure(EntityTypeBuilder<VendorProduct> builder)
    {
        builder.ToTable("VendorProduct");

        builder.HasKey(vp => vp.Id);

        builder.Property(vp => vp.SellingPrice)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(vp => vp.CompareAtPrice)
            .HasPrecision(18, 2);

        builder.Property(vp => vp.StockQuantity)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(vp => vp.IsAvailable)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(vp => vp.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(vp => vp.CustomNameAr)
            .HasMaxLength(200);

        builder.Property(vp => vp.CustomNameEn)
            .HasMaxLength(200);

        builder.Property(vp => vp.CustomDescriptionAr)
            .HasMaxLength(1000);

        builder.Property(vp => vp.CustomDescriptionEn)
            .HasMaxLength(1000);

        // Relationships
        builder.HasOne(vp => vp.Vendor)
            .WithMany()
            .HasForeignKey(vp => vp.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(vp => vp.MasterProduct)
            .WithMany()
            .HasForeignKey(vp => vp.MasterProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(vp => vp.VendorBranch)
            .WithMany()
            .HasForeignKey(vp => vp.VendorBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique: one vendor product per vendor per master product
        builder.HasIndex(vp => new { vp.VendorId, vp.MasterProductId })
            .IsUnique()
            .HasDatabaseName("IX_VendorProduct_Vendor_Master");

        builder.HasIndex(vp => vp.VendorId)
            .HasDatabaseName("IX_VendorProduct_VendorId");

        builder.HasIndex(vp => vp.MasterProductId)
            .HasDatabaseName("IX_VendorProduct_MasterProductId");

        builder.HasIndex(vp => vp.VendorBranchId)
            .HasDatabaseName("IX_VendorProduct_VendorBranchId");
    }
}
