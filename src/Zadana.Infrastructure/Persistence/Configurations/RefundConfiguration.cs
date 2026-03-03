using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Payments.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(300);
        
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
    }
}
