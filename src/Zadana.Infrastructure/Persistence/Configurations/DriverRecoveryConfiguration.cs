using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DriverRecoveryConfiguration : IEntityTypeConfiguration<DriverRecovery>
{
    public void Configure(EntityTypeBuilder<DriverRecovery> builder)
    {
        builder.ToTable("DriverRecoveries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TargetAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.RecoveredAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.OutstandingAmount).HasPrecision(18, 2).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Source)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.OrderSupportCaseId)
            .IsUnique();

        builder.HasIndex(x => new { x.DriverId, x.Status });
    }
}
