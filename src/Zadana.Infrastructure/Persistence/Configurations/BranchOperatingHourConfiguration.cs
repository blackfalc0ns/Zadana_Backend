using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class BranchOperatingHourConfiguration : IEntityTypeConfiguration<BranchOperatingHour>
{
    public void Configure(EntityTypeBuilder<BranchOperatingHour> builder)
    {
        builder.ToTable("BranchOperatingHour");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.DayOfWeek)
            .IsRequired();

        builder.Property(h => h.OpenTime)
            .IsRequired();

        builder.Property(h => h.CloseTime)
            .IsRequired();

        builder.Property(h => h.IsClosed)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasOne(h => h.Branch)
            .WithMany(b => b.OperatingHours)
            .HasForeignKey(h => h.BranchId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique: one entry per branch per day
        builder.HasIndex(h => new { h.BranchId, h.DayOfWeek })
            .IsUnique()
            .HasDatabaseName("IX_BranchOpHour_Branch_Day");
    }
}
