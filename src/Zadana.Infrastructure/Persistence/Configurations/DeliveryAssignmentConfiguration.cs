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
        builder.Property(x => x.CodAmount).HasPrecision(18, 2).HasDefaultValue(0);

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
