using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class AdminBrandBulkOperationConfiguration : IEntityTypeConfiguration<AdminBrandBulkOperation>
{
    public void Configure(EntityTypeBuilder<AdminBrandBulkOperation> builder)
    {
        builder.ToTable("AdminBrandBulkOperations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Operation)
            .HasForeignKey(x => x.OperationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.AdminUserId, x.IdempotencyKey })
            .IsUnique();
    }
}
