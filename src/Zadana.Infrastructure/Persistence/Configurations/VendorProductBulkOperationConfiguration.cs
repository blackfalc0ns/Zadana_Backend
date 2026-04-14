using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class VendorProductBulkOperationConfiguration : IEntityTypeConfiguration<VendorProductBulkOperation>
{
    public void Configure(EntityTypeBuilder<VendorProductBulkOperation> builder)
    {
        builder.ToTable("VendorProductBulkOperation");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        builder.HasOne<Zadana.Domain.Modules.Vendors.Entities.Vendor>()
            .WithMany()
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Operation)
            .HasForeignKey(x => x.OperationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.VendorId);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}
