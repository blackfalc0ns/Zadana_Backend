using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class VendorProfileReviewItemConfiguration : IEntityTypeConfiguration<VendorProfileReviewItem>
{
    public void Configure(EntityTypeBuilder<VendorProfileReviewItem> builder)
    {
        builder.ToTable("VendorProfileReviewItems");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Code)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(item => item.TargetType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(item => item.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(item => item.DecisionNote)
            .HasMaxLength(2000);

        builder.Property(item => item.ReviewedByName)
            .HasMaxLength(200);

        builder.HasIndex(item => new { item.VendorId, item.Code })
            .IsUnique()
            .HasDatabaseName("IX_VendorProfileReviewItems_VendorId_Code");

        builder.HasOne(item => item.Vendor)
            .WithMany(vendor => vendor.ProfileReviewItems)
            .HasForeignKey(item => item.VendorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
