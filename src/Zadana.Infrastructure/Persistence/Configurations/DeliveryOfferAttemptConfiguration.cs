using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DeliveryOfferAttemptConfiguration : IEntityTypeConfiguration<DeliveryOfferAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryOfferAttempt> builder)
    {
        builder.ToTable("DeliveryOfferAttempts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.RejectionReason).HasMaxLength(100);

        builder.HasIndex(x => new { x.OrderId, x.AttemptNumber });
        builder.HasIndex(x => new { x.OrderId, x.DriverId, x.Status });
    }
}
