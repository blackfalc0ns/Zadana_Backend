using Microsoft.EntityFrameworkCore.Migrations;

namespace Zadana.Infrastructure.Data;

internal static class SaudiGeographyMigrationBuilder
{
    public static void Apply(MigrationBuilder migrationBuilder)
    {
        foreach (var region in SaudiGeographyCatalog.Regions)
        {
            migrationBuilder.Sql($"""
IF NOT EXISTS (SELECT 1 FROM [SaudiRegions] WHERE [Code] = N'{Escape(region.Code)}')
BEGIN
    INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
    VALUES ('{region.Id}', N'{Escape(region.Code)}', N'{Escape(region.NameAr)}', N'{Escape(region.NameEn)}', {region.Latitude}, {region.Longitude}, {region.MapZoom}, {region.SortOrder}, SYSUTCDATETIME(), SYSUTCDATETIME());
END
""");
        }

        foreach (var city in SaudiGeographyCatalog.Cities)
        {
            migrationBuilder.Sql($"""
IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'{Escape(city.Code)}')
BEGIN
    INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
    SELECT NEWID(), [Id], N'{Escape(city.Code)}', N'{Escape(city.NameAr)}', N'{Escape(city.NameEn)}', {city.Latitude}, {city.Longitude}, {city.MapZoom}, {city.SortOrder}, SYSUTCDATETIME(), SYSUTCDATETIME()
    FROM [SaudiRegions]
    WHERE [Code] = N'{Escape(city.RegionCode)}';
END
""");
        }
    }

    private static string Escape(string value) => value.Replace("'", "''");
}
