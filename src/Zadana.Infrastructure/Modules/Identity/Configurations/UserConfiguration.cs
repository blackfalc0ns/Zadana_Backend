using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Infrastructure.Modules.Identity.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.Phone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.AccountStatus)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.HasIndex(u => u.Phone)
            .IsUnique();

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(r => r.User)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
