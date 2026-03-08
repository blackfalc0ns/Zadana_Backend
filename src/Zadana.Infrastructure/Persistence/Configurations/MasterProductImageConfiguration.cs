using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class MasterProductImageConfiguration : IEntityTypeConfiguration<MasterProductImage>
{
    public void Configure(EntityTypeBuilder<MasterProductImage> builder)
    {
        builder.ToTable("MasterProductImage");

        // Primary key (can be an Id or composite if we wanted to enforce one URL per product, but we'll use a surrogate or just identity. Wait, the domain model currently doesn't have an `Id` property. Let me check the domain model.)
        // Since MasterProductImage is essentially an owned entity or has no primary key, I'll use a shadow key or add an Id property.
        // Let's add an Id property to the entity, or just use a composite key of MasterProductId and Url.
        builder.HasKey(mpi => new { mpi.MasterProductId, mpi.Url });

        builder.Property(mpi => mpi.DisplayOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(mpi => mpi.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(mpi => mpi.Url)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(mpi => mpi.AltText)
            .HasMaxLength(500);

        // Relationships
        builder.HasOne(mpi => mpi.MasterProduct)
            .WithMany(mp => mp.Images)
            .HasForeignKey(mpi => mpi.MasterProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
