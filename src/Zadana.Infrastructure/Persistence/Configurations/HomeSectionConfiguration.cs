using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class HomeSectionConfiguration : IEntityTypeConfiguration<HomeSection>
{
    public void Configure(EntityTypeBuilder<HomeSection> builder)
    {
        builder.ToTable("HomeSection", table =>
        {
            table.HasCheckConstraint(
                "CK_HomeSection_Theme",
                $"[Theme] IN ('{HomeSectionThemeCatalog.SoftBlueKey}', '{HomeSectionThemeCatalog.FreshOrangeKey}', '{HomeSectionThemeCatalog.BoldDarkKey}')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Theme)
            .HasConversion(
                theme => theme.ToKey(),
                value => HomeSectionThemeCatalog.ParseOrDefault(value, HomeSectionTheme.SoftBlue))
            .HasMaxLength(32)
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
