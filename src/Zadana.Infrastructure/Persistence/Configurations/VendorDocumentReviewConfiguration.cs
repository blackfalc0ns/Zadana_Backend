using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class VendorDocumentReviewConfiguration : IEntityTypeConfiguration<VendorDocumentReview>
{
    public void Configure(EntityTypeBuilder<VendorDocumentReview> builder)
    {
        builder.ToTable("VendorDocumentReviews");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(item => item.Decision)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(item => item.RejectionReason)
            .HasMaxLength(1000);

        builder.Property(item => item.ReviewedByName)
            .HasMaxLength(200);

        builder.HasOne(item => item.Vendor)
            .WithMany(vendor => vendor.DocumentReviews)
            .HasForeignKey(item => item.VendorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.VendorId, item.Type })
            .IsUnique()
            .HasDatabaseName("IX_VendorDocumentReviews_VendorId_Type");
    }
}
