using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DeliveryPricingRuleConfiguration : IEntityTypeConfiguration<DeliveryPricingRule>
{
    public void Configure(EntityTypeBuilder<DeliveryPricingRule> builder)
    {
        builder.ToTable("DeliveryPricingRules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.City).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.BaseFee).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.IncludedKm).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.PerKmFee).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.MinFee).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.MaxFee).HasPrecision(18, 2).IsRequired();

        builder.HasIndex(x => new { x.City, x.IsActive });
        builder.HasIndex(x => new { x.DeliveryZoneId, x.IsActive });

        builder.HasOne(x => x.DeliveryZone)
            .WithMany()
            .HasForeignKey(x => x.DeliveryZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.SurgeWindows)
            .WithOne(x => x.DeliveryPricingRule)
            .HasForeignKey(x => x.DeliveryPricingRuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
