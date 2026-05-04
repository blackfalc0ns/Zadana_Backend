using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class UserPermissionOverrideConfiguration : IEntityTypeConfiguration<UserPermissionOverride>
{
    public void Configure(EntityTypeBuilder<UserPermissionOverride> builder)
    {
        builder.ToTable("UserPermissionOverrides");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PermissionKey)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Mode)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Reason)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.UserId, x.PermissionKey, x.IsActive });
    }
}
