using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class VendorStaffInvitationConfiguration : IEntityTypeConfiguration<VendorStaffInvitation>
{
    public void Configure(EntityTypeBuilder<VendorStaffInvitation> builder)
    {
        builder.ToTable("VendorStaffInvitations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.TargetName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.RoleTemplate)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.BranchIdsJson)
            .IsRequired();

        builder.Property(x => x.PermissionsJson)
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.InviteMessage)
            .HasMaxLength(1000);

        builder.Property(x => x.ProviderMessageId)
            .HasMaxLength(200);

        builder.Property(x => x.LastSendFailureReason)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.HasIndex(x => new { x.VendorId, x.Email, x.Status });

        builder.HasOne(x => x.Vendor)
            .WithMany()
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
