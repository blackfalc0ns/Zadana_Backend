using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class SettlementItemConfiguration : IEntityTypeConfiguration<SettlementItem>
{
    public void Configure(EntityTypeBuilder<SettlementItem> builder)
    {
        builder.ToTable("SettlementItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.VendorAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.DriverAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.PlatformCommission).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CodCollectedAmount).HasPrecision(18, 2).IsRequired();

        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
