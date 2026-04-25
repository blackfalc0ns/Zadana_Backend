using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class VendorWorkspaceStateConfiguration : IEntityTypeConfiguration<VendorWorkspaceState>
{
    public void Configure(EntityTypeBuilder<VendorWorkspaceState> builder)
    {
        builder.ToTable("VendorWorkspaceStates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Feature)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.PayloadJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.HasIndex(x => new { x.VendorId, x.Feature })
            .IsUnique();

        builder.HasOne(x => x.Vendor)
            .WithMany()
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
