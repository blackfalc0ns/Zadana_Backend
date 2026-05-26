using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverLatestLocationAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DriverLocations_DriverId",
                table: "DriverLocations");

            migrationBuilder.CreateTable(
                name: "DriverLatestLocations",
                columns: table => new
                {
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: false),
                    AccuracyMeters = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverLatestLocations", x => x.DriverId);
                    table.ForeignKey(
                        name: "FK_DriverLatestLocations_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverLocations_DriverId_RecordedAt_Desc",
                table: "DriverLocations",
                columns: new[] { "DriverId", "RecordedAtUtc" },
                descending: new[] { false, true });

            // Backfill the new latest-location projection from the audit
            // history so the read paths see consistent data immediately
            // after deployment. The CTE picks the newest row per driver and
            // upserts it into DriverLatestLocations.
            migrationBuilder.Sql(@"
                ;WITH Latest AS (
                    SELECT
                        DriverId,
                        Latitude,
                        Longitude,
                        AccuracyMeters,
                        RecordedAtUtc,
                        ROW_NUMBER() OVER (PARTITION BY DriverId ORDER BY RecordedAtUtc DESC) AS rn
                    FROM dbo.DriverLocations
                )
                INSERT INTO dbo.DriverLatestLocations
                    (DriverId, Latitude, Longitude, AccuracyMeters, RecordedAtUtc, UpdatedAtUtc)
                SELECT
                    DriverId, Latitude, Longitude, AccuracyMeters, RecordedAtUtc, SYSUTCDATETIME()
                FROM Latest
                WHERE rn = 1
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.DriverLatestLocations l WHERE l.DriverId = Latest.DriverId
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverLatestLocations");

            migrationBuilder.DropIndex(
                name: "IX_DriverLocations_DriverId_RecordedAt_Desc",
                table: "DriverLocations");

            migrationBuilder.CreateIndex(
                name: "IX_DriverLocations_DriverId",
                table: "DriverLocations",
                column: "DriverId");
        }
    }
}
