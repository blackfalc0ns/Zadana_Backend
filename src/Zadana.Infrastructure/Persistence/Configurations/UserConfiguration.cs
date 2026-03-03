using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.Phone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

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

        builder.Property(u => u.IsEmailVerified)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.IsPhoneVerified)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.LastLoginAtUtc);

        // Indexes
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_User_Email");

        builder.HasIndex(u => u.Phone)
            .IsUnique()
            .HasDatabaseName("IX_User_Phone");
    }
}
