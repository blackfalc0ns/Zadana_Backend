using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zadana.Infrastructure.Persistence;

#nullable disable

namespace Zadana.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260423184500_NormalizeLegacyDriverVehicleTypes")]
public partial class NormalizeLegacyDriverVehicleTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE [Drivers]
            SET [VehicleType] = CASE
                WHEN [VehicleType] IS NULL THEN NULL
                WHEN LOWER(LTRIM(RTRIM([VehicleType]))) = 'bike' THEN 'Bicycle'
                WHEN LOWER(LTRIM(RTRIM([VehicleType]))) = 'cargo van' THEN 'Van'
                WHEN LOWER(LTRIM(RTRIM([VehicleType]))) = 'motorbike' THEN 'Motorcycle'
                ELSE [VehicleType]
            END
            WHERE [VehicleType] IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Data normalization is intentionally irreversible.
    }
}
