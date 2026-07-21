using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class SettlementProcessingSettingsConfiguration : IEntityTypeConfiguration<SettlementProcessingSettings>
{
    public void Configure(EntityTypeBuilder<SettlementProcessingSettings> builder)
    {
        builder.ToTable("SettlementProcessingSettings");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Mode)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(SettlementProcessingMode.Automatic)
            .IsRequired();

        builder.Property(item => item.UpdatedAtUtc)
            .IsRequired();
    }
}
