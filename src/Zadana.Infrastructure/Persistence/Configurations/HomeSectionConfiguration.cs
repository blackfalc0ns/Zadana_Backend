using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Marketing.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class HomeSectionConfiguration : IEntityTypeConfiguration<HomeSection>
{
    public void Configure(EntityTypeBuilder<HomeSection> builder)
    {
        builder.ToTable("HomeSection");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Theme)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0);

        builder.Property(x => x.ProductsTake)
            .HasDefaultValue(10);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.IsActive, x.DisplayOrder })
            .HasDatabaseName("IX_HomeSection_IsActive_DisplayOrder");

        builder.HasIndex(x => x.CategoryId)
            .HasDatabaseName("IX_HomeSection_CategoryId");
    }
}
