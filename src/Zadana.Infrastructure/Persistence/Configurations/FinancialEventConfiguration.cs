using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Finances.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class FinancialEventConfiguration : IEntityTypeConfiguration<FinancialEvent>
{
    public void Configure(EntityTypeBuilder<FinancialEvent> builder)
    {
        builder.ToTable("FinancialEvents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType)
            .HasConversion<string>()
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired()
            .HasDefaultValue("EGP");

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_FinancialEvents_IdempotencyKey");

        builder.HasIndex(x => x.CorrelationId)
            .HasDatabaseName("IX_FinancialEvents_CorrelationId");

        builder.HasIndex(x => x.OrderId)
            .HasDatabaseName("IX_FinancialEvents_OrderId");
    }
}
