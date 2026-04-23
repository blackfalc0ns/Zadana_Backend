using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DriverIncidentConfiguration : IEntityTypeConfiguration<DriverIncident>
{
    public void Configure(EntityTypeBuilder<DriverIncident> builder)
    {
        builder.ToTable("DriverIncidents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IncidentType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReviewerName).HasMaxLength(200);
        builder.Property(x => x.Summary).HasMaxLength(1000).IsRequired();
    }
}
