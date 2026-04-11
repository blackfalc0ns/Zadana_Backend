using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(u => u.AccountStatus)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(u => u.PresenceState)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(u => u.IsLoginLocked)
            .HasDefaultValue(false);

        builder.Property(u => u.LockReason)
            .HasMaxLength(500);

        builder.Property(u => u.ArchiveReason)
            .HasMaxLength(500);

        builder.Property(u => u.Latitude)
            .HasPrecision(9, 6);

        builder.Property(u => u.Longitude)
            .HasPrecision(9, 6);

        builder.Property(u => u.LastLoginAtUtc);
        builder.Property(u => u.LastSeenAtUtc);
        builder.Property(u => u.LockedAtUtc);
        builder.Property(u => u.ArchivedAtUtc);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(r => r.User)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
