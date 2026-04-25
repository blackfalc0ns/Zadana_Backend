using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DeliveryPricingSurgeWindowConfiguration : IEntityTypeConfiguration<DeliveryPricingSurgeWindow>
{
    public void Configure(EntityTypeBuilder<DeliveryPricingSurgeWindow> builder)
    {
        builder.ToTable("DeliveryPricingSurgeWindows");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Multiplier).HasPrecision(8, 2).IsRequired();

        builder.HasIndex(x => new { x.DeliveryPricingRuleId, x.IsActive });
    }
}
