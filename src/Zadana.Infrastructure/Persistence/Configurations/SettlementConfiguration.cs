using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> builder)
    {
        builder.ToTable("Settlements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Origin)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.OwnerType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.OwnerId).IsRequired();
        builder.Property(x => x.ResolutionType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.PeriodFrom).IsRequired();
        builder.Property(x => x.PeriodTo).IsRequired();

        builder.Property(x => x.GrossAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CommissionAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.RefundAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.AdjustmentAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.RecoveryAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.NetAmount).HasPrecision(18, 2).IsRequired();

        builder.HasIndex(x => new { x.OwnerType, x.OwnerId, x.PeriodFrom, x.PeriodTo })
            .HasDatabaseName("IX_Settlements_Owner_Period");

        builder.HasOne(x => x.Vendor)
            .WithMany()
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Driver)
            .WithMany()
            .HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Settlement)
            .HasForeignKey(x => x.SettlementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Payouts)
            .WithOne(x => x.Settlement)
            .HasForeignKey(x => x.SettlementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
