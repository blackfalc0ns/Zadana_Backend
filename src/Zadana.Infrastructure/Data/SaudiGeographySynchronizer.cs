using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Geography.Entities;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Data;

public sealed class SaudiGeographySynchronizer(ApplicationDbContext dbContext)
{
    public async Task<SaudiGeographySyncResult> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var regionsInserted = 0;
        var regionsUpdated = 0;
        var citiesInserted = 0;

        var existingRegions = await dbContext.SaudiRegions
            .ToDictionaryAsync(region => region.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var seed in SaudiGeographyCatalog.Regions)
        {
            if (existingRegions.TryGetValue(seed.Code, out var region))
            {
                if (region.NameAr != seed.NameAr || region.NameEn != seed.NameEn)
                {
                    regionsUpdated++;
                }

                continue;
            }

            dbContext.SaudiRegions.Add(new SaudiRegion(
                seed.Id,
                seed.Code,
                seed.NameAr,
                seed.NameEn,
                seed.Latitude,
                seed.Longitude,
                seed.MapZoom,
                seed.SortOrder));

            existingRegions[seed.Code] = dbContext.SaudiRegions.Local
                .First(entity => entity.Code == seed.Code);
            regionsInserted++;
        }

        if (regionsInserted > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var regionByCode = await dbContext.SaudiRegions
            .AsNoTracking()
            .ToDictionaryAsync(region => region.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existingCityCodes = await dbContext.SaudiCities
            .AsNoTracking()
            .Select(city => city.Code)
            .ToListAsync(cancellationToken);

        var existingCodes = new HashSet<string>(existingCityCodes, StringComparer.OrdinalIgnoreCase);

        foreach (var seed in SaudiGeographyCatalog.Cities)
        {
            if (existingCodes.Contains(seed.Code))
            {
                continue;
            }

            if (!regionByCode.TryGetValue(seed.RegionCode, out var region))
            {
                continue;
            }

            dbContext.SaudiCities.Add(new SaudiCity(
                Guid.NewGuid(),
                region.Id,
                seed.Code,
                seed.NameAr,
                seed.NameEn,
                seed.Latitude,
                seed.Longitude,
                seed.MapZoom,
                seed.SortOrder));

            existingCodes.Add(seed.Code);
            citiesInserted++;
        }

        if (citiesInserted > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new SaudiGeographySyncResult(regionsInserted, regionsUpdated, citiesInserted);
    }
}

public sealed record SaudiGeographySyncResult(int RegionsInserted, int RegionsUpdated, int CitiesInserted);
