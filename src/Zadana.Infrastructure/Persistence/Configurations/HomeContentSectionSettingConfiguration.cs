using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Marketing.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class HomeContentSectionSettingConfiguration : IEntityTypeConfiguration<HomeContentSectionSetting>
{
    public void Configure(EntityTypeBuilder<HomeContentSectionSetting> builder)
    {
        builder.ToTable("HomeContentSectionSetting");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SectionType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.IsEnabled)
            .HasDefaultValue(true);

        builder.HasIndex(x => x.SectionType)
            .IsUnique()
            .HasDatabaseName("IX_HomeContentSectionSetting_SectionType");
    }
}
