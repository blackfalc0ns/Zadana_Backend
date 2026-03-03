using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ToTable("UnitOfMeasure");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.NameAr)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.NameEn)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);
    }
}
