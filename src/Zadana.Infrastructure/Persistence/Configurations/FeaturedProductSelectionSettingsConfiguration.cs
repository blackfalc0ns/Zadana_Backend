using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Marketing.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class FeaturedProductSelectionSettingsConfiguration : IEntityTypeConfiguration<FeaturedProductSelectionSettings>
{
    public void Configure(EntityTypeBuilder<FeaturedProductSelectionSettings> builder)
    {
        builder.ToTable("FeaturedProductSelectionSettings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SelectionMode)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.TargetCount)
            .HasDefaultValue(FeaturedProductSelectionSettings.DefaultTargetCount);

        builder.Property(x => x.MinSalesCount)
            .HasDefaultValue(FeaturedProductSelectionSettings.DefaultMinSalesCount);

        builder.Property(x => x.MinStoreCount)
            .HasDefaultValue(FeaturedProductSelectionSettings.DefaultMinStoreCount);

        builder.Property(x => x.RequireDiscount)
            .HasDefaultValue(FeaturedProductSelectionSettings.DefaultRequireDiscount);

        builder.Property(x => x.ExcludeProductsAlreadyInSpecialOffers)
            .HasDefaultValue(FeaturedProductSelectionSettings.DefaultExcludeProductsAlreadyInSpecialOffers);
    }
}
