using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class SettlementProcessingModeAuditConfiguration : IEntityTypeConfiguration<SettlementProcessingModeAudit>
{
    public void Configure(EntityTypeBuilder<SettlementProcessingModeAudit> builder)
    {
        builder.ToTable("SettlementProcessingModeAudits");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.PreviousMode)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(item => item.NewMode)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(item => item.ChangedAtUtc)
            .HasDatabaseName("IX_SettlementProcessingModeAudits_ChangedAtUtc");
    }
}
