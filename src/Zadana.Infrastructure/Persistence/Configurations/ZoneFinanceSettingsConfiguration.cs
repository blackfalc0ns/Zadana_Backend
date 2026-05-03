using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Finances.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class ZoneFinanceSettingsConfiguration : IEntityTypeConfiguration<ZoneFinanceSettings>
{
    public void Configure(EntityTypeBuilder<ZoneFinanceSettings> builder)
    {
        builder.ToTable("ZoneFinanceSettings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeliveryZoneId)
            .IsRequired();

        builder.Property(x => x.VatPercent)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.CodFeeType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CodFlatFee)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.CodPercent)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.HasIndex(x => x.DeliveryZoneId)
            .IsUnique(); // One-to-One mapping conceptually with DeliveryZone
    }
}
