using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class BrandRequestConfiguration : IEntityTypeConfiguration<BrandRequest>
{
    public void Configure(EntityTypeBuilder<BrandRequest> builder)
    {
        builder.ToTable("BrandRequest");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NameAr)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.CategoryId)
            .IsRequired();

        builder.Property(x => x.NameEn)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.LogoUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(500);

        builder.Property(x => x.ReviewedBy)
            .HasMaxLength(200);

        builder.HasOne(x => x.Vendor)
            .WithMany()
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedBrand)
            .WithMany()
            .HasForeignKey(x => x.CreatedBrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CategoryId).HasDatabaseName("IX_BrandRequest_CategoryId");
        builder.HasIndex(x => x.VendorId).HasDatabaseName("IX_BrandRequest_VendorId");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_BrandRequest_Status");
    }
}
