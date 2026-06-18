using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DeliveryAssignmentConfiguration : IEntityTypeConfiguration<DeliveryAssignment>
{
    public void Configure(EntityTypeBuilder<DeliveryAssignment> builder)
    {
        builder.ToTable("DeliveryAssignments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.FailureReason).HasMaxLength(300);
        builder.Property(x => x.OfferRejectedReason).HasMaxLength(100);
        builder.Property(x => x.CodAmount).HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(x => x.PickupOtpCode).HasMaxLength(10);
        builder.Property(x => x.DeliveryOtpCode).HasMaxLength(10);

        builder.HasIndex(x => new { x.OrderId, x.Status, x.CreatedAtUtc })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_DeliveryAssignments_OrderId_Status_CreatedAt_Desc");
        builder.HasIndex(x => new { x.DriverId, x.Status, x.CreatedAtUtc })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_DeliveryAssignments_DriverId_Status_CreatedAt_Desc");
        builder.HasIndex(x => new { x.OrderId, x.CreatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_DeliveryAssignments_OrderId_CreatedAt_Desc");
        builder.HasIndex(x => new { x.Status, x.OfferExpiresAtUtc })
            .HasDatabaseName("IX_DeliveryAssignments_Status_OfferExpiresAtUtc");

        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Proofs)
            .WithOne(x => x.Assignment)
            .HasForeignKey(x => x.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
