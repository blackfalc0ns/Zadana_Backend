using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class AdminBrandBulkOperationItemConfiguration : IEntityTypeConfiguration<AdminBrandBulkOperationItem>
{
    public void Configure(EntityTypeBuilder<AdminBrandBulkOperationItem> builder)
    {
        builder.ToTable("AdminBrandBulkOperationItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NameAr)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.NameEn)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.LogoUrl)
            .HasMaxLength(500);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.OperationId);
    }
}
