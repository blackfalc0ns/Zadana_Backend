using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class VendorProductBulkOperationItemConfiguration : IEntityTypeConfiguration<VendorProductBulkOperationItem>
{
    public void Configure(EntityTypeBuilder<VendorProductBulkOperationItem> builder)
    {
        builder.ToTable("VendorProductBulkOperationItem");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SellingPrice)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(x => x.CompareAtPrice)
            .HasPrecision(18, 2);

        builder.Property(x => x.Sku)
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        builder.HasOne(x => x.MasterProduct)
            .WithMany()
            .HasForeignKey(x => x.MasterProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VendorBranch)
            .WithMany()
            .HasForeignKey(x => x.VendorBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OperationId);
        builder.HasIndex(x => new { x.OperationId, x.RowNumber }).IsUnique();
        builder.HasIndex(x => x.MasterProductId);
    }
}
