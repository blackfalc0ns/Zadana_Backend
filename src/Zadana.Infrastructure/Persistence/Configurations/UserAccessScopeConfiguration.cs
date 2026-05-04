using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class UserAccessScopeConfiguration : IEntityTypeConfiguration<UserAccessScope>
{
    public void Configure(EntityTypeBuilder<UserAccessScope> builder)
    {
        builder.ToTable("UserAccessScopes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PanelScope)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.ScopeType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.UserId, x.IsActive });

        builder.HasOne(x => x.RoleDefinition)
            .WithMany(x => x.UserAccessScopes)
            .HasForeignKey(x => x.RoleDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
