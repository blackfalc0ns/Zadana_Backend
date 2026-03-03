using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class MasterProductImageConfiguration : IEntityTypeConfiguration<MasterProductImage>
{
    public void Configure(EntityTypeBuilder<MasterProductImage> builder)
    {
        builder.ToTable("MasterProductImage");

        // Composite primary key
        builder.HasKey(mpi => new { mpi.MasterProductId, mpi.ImageBankId });

        builder.Property(mpi => mpi.DisplayOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(mpi => mpi.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasOne(mpi => mpi.MasterProduct)
            .WithMany(mp => mp.Images)
            .HasForeignKey(mpi => mpi.MasterProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mpi => mpi.ImageBank)
            .WithMany(ib => ib.ProductUsages)
            .HasForeignKey(mpi => mpi.ImageBankId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
