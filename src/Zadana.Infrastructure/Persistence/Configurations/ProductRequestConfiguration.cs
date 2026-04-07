using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class ProductRequestConfiguration : IEntityTypeConfiguration<ProductRequest>
{
    public void Configure(EntityTypeBuilder<ProductRequest> builder)
    {
        builder.ToTable("ProductRequest");

        builder.HasKey(pr => pr.Id);

        builder.Property(pr => pr.SuggestedNameAr)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(pr => pr.SuggestedNameEn)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(pr => pr.SuggestedDescriptionAr)
            .HasMaxLength(1000);

        builder.Property(pr => pr.SuggestedDescriptionEn)
            .HasMaxLength(1000);

        builder.Property(pr => pr.ImageUrl)
            .HasMaxLength(1000);

        builder.Property(pr => pr.ReviewedBy)
            .HasMaxLength(200);

        builder.Property(pr => pr.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(pr => pr.RejectionReason)
            .HasMaxLength(500);

        // Relationships
        builder.HasOne(pr => pr.Vendor)
            .WithMany()
            .HasForeignKey(pr => pr.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pr => pr.Category)
            .WithMany()
            .HasForeignKey(pr => pr.SuggestedCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pr => pr.CategoryRequest)
            .WithMany()
            .HasForeignKey(pr => pr.SuggestedCategoryRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pr => pr.Brand)
            .WithMany()
            .HasForeignKey(pr => pr.SuggestedBrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pr => pr.BrandRequest)
            .WithMany()
            .HasForeignKey(pr => pr.SuggestedBrandRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pr => pr.UnitOfMeasure)
            .WithMany()
            .HasForeignKey(pr => pr.SuggestedUnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pr => pr.CreatedMasterProduct)
            .WithMany()
            .HasForeignKey(pr => pr.CreatedMasterProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(pr => pr.VendorId)
            .HasDatabaseName("IX_ProductRequest_VendorId");

        builder.HasIndex(pr => pr.Status)
            .HasDatabaseName("IX_ProductRequest_Status");
    }
}
