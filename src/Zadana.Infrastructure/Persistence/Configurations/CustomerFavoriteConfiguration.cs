using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class CustomerFavoriteConfiguration : IEntityTypeConfiguration<CustomerFavorite>
{
    public void Configure(EntityTypeBuilder<CustomerFavorite> builder)
    {
        builder.ToTable("CustomerFavorites");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GuestId).HasMaxLength(200);

        builder.HasIndex(x => new { x.UserId, x.MasterProductId })
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");

        builder.HasIndex(x => new { x.GuestId, x.MasterProductId })
            .IsUnique()
            .HasFilter("[GuestId] IS NOT NULL");

        builder.HasIndex(x => x.MasterProductId);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MasterProduct)
            .WithMany()
            .HasForeignKey(x => x.MasterProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
