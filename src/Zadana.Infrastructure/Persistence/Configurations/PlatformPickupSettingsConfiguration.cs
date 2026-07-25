using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class PlatformPickupSettingsConfiguration : IEntityTypeConfiguration<PlatformPickupSettings>
{
    public void Configure(EntityTypeBuilder<PlatformPickupSettings> builder)
    {
        builder.ToTable("PlatformPickupSettings");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.PickupCommissionPercent).HasPrecision(5, 2).IsRequired();
        builder.Property(item => item.PickupNoShowTimeoutHours).IsRequired();
        builder.Property(item => item.PickupOtpMaxAttempts).IsRequired();
        builder.Property(item => item.PickupOtpLockoutMinutes).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();
    }
}
