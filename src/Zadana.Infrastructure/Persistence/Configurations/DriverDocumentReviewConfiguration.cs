using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DriverDocumentReviewConfiguration : IEntityTypeConfiguration<DriverDocumentReview>
{
    public void Configure(EntityTypeBuilder<DriverDocumentReview> builder)
    {
        builder.ToTable("DriverDocumentReviews");

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

        builder.HasOne(item => item.Driver)
            .WithMany(driver => driver.DocumentReviews)
            .HasForeignKey(item => item.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.DriverId, item.Type })
            .IsUnique()
            .HasDatabaseName("IX_DriverDocumentReviews_DriverId_Type");
    }
}
