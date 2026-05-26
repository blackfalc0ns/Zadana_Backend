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

        builder.Property(u => u.PermissionVersion)
            .HasDefaultValue(1);

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

        builder.Property(u => u.MustChangePassword)
            .HasDefaultValue(false);

        builder.Property(u => u.LockReason)
            .HasMaxLength(500);

        builder.Property(u => u.ArchiveReason)
            .HasMaxLength(500);

        builder.Property(u => u.Department)
            .HasMaxLength(100);

        builder.Property(u => u.Team)
            .HasMaxLength(100);

        builder.Property(u => u.Latitude)
            .HasPrecision(9, 6);

        builder.Property(u => u.Longitude)
            .HasPrecision(9, 6);

        builder.Property(u => u.LastLoginAtUtc);
        builder.Property(u => u.LastSeenAtUtc);
        builder.Property(u => u.LockedAtUtc);
        builder.Property(u => u.ArchivedAtUtc);
        builder.Property(u => u.TemporaryPasswordIssuedAtUtc);
        builder.Property(u => u.LastPasswordChangedAtUtc);

        // OTP fields. The codes are now stored as SHA-256 hex digests
        // (64 chars) rather than the raw 4-digit code.
        builder.Property(u => u.OtpCode)
            .HasMaxLength(128);
        builder.Property(u => u.OtpAttempts)
            .HasDefaultValue(0);
        builder.Property(u => u.OtpLockoutCount)
            .HasDefaultValue(0);
        builder.Property(u => u.OtpLockedUntilUtc);
        builder.Property(u => u.PasswordResetOtp)
            .HasMaxLength(128);
        builder.Property(u => u.PasswordResetOtpAttempts)
            .HasDefaultValue(0);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(r => r.User)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<UserAccessScope>()
            .WithOne(scope => scope.User)
            .HasForeignKey(scope => scope.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<UserPermissionOverride>()
            .WithOne(overrideEntry => overrideEntry.User)
            .HasForeignKey(overrideEntry => overrideEntry.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<AccessAuditLog>()
            .WithOne(log => log.TargetUser)
            .HasForeignKey(log => log.TargetUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
