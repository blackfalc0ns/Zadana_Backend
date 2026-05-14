using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Finances.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DeliveryPricingDefaultsConfiguration : IEntityTypeConfiguration<DeliveryPricingDefaults>
{
    public void Configure(EntityTypeBuilder<DeliveryPricingDefaults> builder)
    {
        builder.ToTable("DeliveryPricingDefaults");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.CodFeeType).HasMaxLength(20).IsRequired();
        builder.Property(item => item.BaseDeliveryFee).HasPrecision(18, 2);
        builder.Property(item => item.IncludedKm).HasPrecision(18, 2);
        builder.Property(item => item.ExtraKmFee).HasPrecision(18, 2);
        builder.Property(item => item.MinDeliveryFee).HasPrecision(18, 2);
        builder.Property(item => item.MaxDeliveryFee).HasPrecision(18, 2);
        builder.Property(item => item.VatPercent).HasPrecision(18, 2);
        builder.Property(item => item.CodFlatFee).HasPrecision(18, 2);
        builder.Property(item => item.CodPercent).HasPrecision(18, 2);
        builder.Property(item => item.MinTotalDeliveryFee).HasPrecision(18, 2);
        builder.Property(item => item.MaxTotalDeliveryFee).HasPrecision(18, 2);
        builder.Property(item => item.MaxQuotedDistanceKm).HasPrecision(18, 2);
        builder.Property(item => item.WarningSubtotalRatioThreshold).HasPrecision(18, 2);
    }
}
