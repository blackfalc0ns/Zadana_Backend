using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class AdminMasterProductBulkOperationItemConfiguration : IEntityTypeConfiguration<AdminMasterProductBulkOperationItem>
{
    public void Configure(EntityTypeBuilder<AdminMasterProductBulkOperationItem> builder)
    {
        builder.ToTable("AdminMasterProductBulkOperationItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NameAr)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.NameEn)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.Slug)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.Barcode)
            .HasMaxLength(100);

        builder.Property(x => x.DescriptionAr)
            .HasMaxLength(2000);

        builder.Property(x => x.DescriptionEn)
            .HasMaxLength(2000);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Brand)
            .WithMany()
            .HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Unit)
            .WithMany()
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.OperationId);
    }
}
