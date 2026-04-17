using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class CategoryRequestConfiguration : IEntityTypeConfiguration<CategoryRequest>
{
    public void Configure(EntityTypeBuilder<CategoryRequest> builder)
    {
        builder.ToTable("CategoryRequest");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NameAr)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.NameEn)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.TargetLevel)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ImageUrl)
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

        builder.HasOne(x => x.ParentCategory)
            .WithMany()
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedCategory)
            .WithMany()
            .HasForeignKey(x => x.CreatedCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.VendorId).HasDatabaseName("IX_CategoryRequest_VendorId");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_CategoryRequest_Status");
    }
}
