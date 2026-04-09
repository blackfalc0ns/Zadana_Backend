using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class MasterProductConfiguration : IEntityTypeConfiguration<MasterProduct>
{
    public void Configure(EntityTypeBuilder<MasterProduct> builder)
    {
        builder.ToTable("MasterProduct");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.NameAr)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(p => p.NameEn)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(p => p.Slug)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(p => p.DescriptionAr)
            .HasMaxLength(2000);

        builder.Property(p => p.DescriptionEn)
            .HasMaxLength(2000);

        builder.Property(p => p.Barcode)
            .HasMaxLength(50);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        // Relationships
        builder.HasOne(p => p.Category)
            .WithMany(c => c.MasterProducts)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Brand)
            .WithMany(b => b.MasterProducts)
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.UnitOfMeasure)
            .WithMany(u => u.MasterProducts)
            .HasForeignKey(p => p.UnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ProductType)
            .WithMany(pt => pt.MasterProducts)
            .HasForeignKey(p => p.ProductTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Part)
            .WithMany(part => part.MasterProducts)
            .HasForeignKey(p => p.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(p => p.Slug)
            .IsUnique()
            .HasDatabaseName("IX_MasterProduct_Slug");

        builder.HasIndex(p => p.Barcode)
            .IsUnique()
            .HasDatabaseName("IX_MasterProduct_Barcode")
            .HasFilter("[Barcode] IS NOT NULL");

        builder.HasIndex(p => p.CategoryId)
            .HasDatabaseName("IX_MasterProduct_CategoryId");

        builder.HasIndex(p => p.BrandId)
            .HasDatabaseName("IX_MasterProduct_BrandId");

        builder.HasIndex(p => p.ProductTypeId)
            .HasDatabaseName("IX_MasterProduct_ProductTypeId");

        builder.HasIndex(p => p.PartId)
            .HasDatabaseName("IX_MasterProduct_PartId");
    }
}
