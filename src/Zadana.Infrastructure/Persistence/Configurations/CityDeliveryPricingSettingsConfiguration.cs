using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Finances.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class CityDeliveryPricingSettingsConfiguration : IEntityTypeConfiguration<CityDeliveryPricingSettings>
{
    public void Configure(EntityTypeBuilder<CityDeliveryPricingSettings> builder)
    {
        builder.ToTable("CityDeliveryPricingSettings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SaudiCityId).IsRequired();
        builder.Property(x => x.BaseDeliveryFee).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.IncludedKm).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.ExtraKmFee).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.MinDeliveryFee).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.MaxDeliveryFee).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.VatPercent).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.CodFeeType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CodFlatFee).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.CodPercent).HasColumnType("decimal(18,2)").IsRequired();

        builder.HasIndex(x => x.SaudiCityId).IsUnique();
    }
}
