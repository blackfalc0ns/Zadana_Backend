using Microsoft.EntityFrameworkCore;

using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Geography.Entities;
using Zadana.Domain.Modules.Vendors.Enums;

using Zadana.SharedKernel.Exceptions;



namespace Zadana.Application.Modules.Geography.Support;



public static class OperationalGeographyScope

{

    public const string EasternRegionCode = "EASTERN";

    public const bool DriverShowsCityPicker = false;



    public static Task EnsureOperationalRegionCityIfProvidedAsync(

        IApplicationDbContext context,

        string? regionCode,

        string? cityCode,

        CancellationToken cancellationToken)

    {

        if (string.IsNullOrWhiteSpace(regionCode) && string.IsNullOrWhiteSpace(cityCode))

        {

            return Task.CompletedTask;

        }



        return EnsureOperationalRegionCityAsync(context, regionCode, cityCode, cancellationToken);

    }



    public static async Task EnsureOperationalRegionCityAsync(

        IApplicationDbContext context,

        string? regionCode,

        string? cityCode,

        CancellationToken cancellationToken)

    {

        var normalizedRegion = NormalizeCode(regionCode);

        if (normalizedRegion.Length == 0)

        {

            throw new BusinessRuleException(

                "SERVICE_REGION_REQUIRED",

                "لازم تختار منطقة التشغيل.");

        }



        if (!await IsOperationalRegionAsync(context, normalizedRegion, cancellationToken))

        {

            throw new BusinessRuleException(

                "UNSUPPORTED_OPERATIONAL_REGION",

                "المنطقة المختارة غير مفعّلة للتشغيل حاليًا.");

        }



        var normalizedCity = NormalizeCode(cityCode);

        if (normalizedCity.Length == 0)

        {

            throw new BusinessRuleException(

                "SERVICE_CITY_REQUIRED",

                "لازم تختار مدينة التشغيل.");

        }



        if (!await IsOperationalCityAsync(context, normalizedRegion, normalizedCity, cancellationToken))

        {

            throw new BusinessRuleException(

                "UNSUPPORTED_OPERATIONAL_CITY",

                "المدينة المختارة غير مفعّلة للتشغيل حاليًا.");

        }

    }



    public static async Task EnsureDriverServiceAreaAsync(

        IApplicationDbContext context,

        string? regionCode,

        CancellationToken cancellationToken)

    {

        var normalizedRegion = NormalizeCode(regionCode);

        if (normalizedRegion.Length == 0)

        {

            throw new BusinessRuleException(

                "SERVICE_REGION_REQUIRED",

                "لازم تختار منطقة التشغيل.");

        }



        if (!await IsOperationalRegionAsync(context, normalizedRegion, cancellationToken))

        {

            throw new BusinessRuleException(

                "UNSUPPORTED_OPERATIONAL_REGION",

                "المنطقة المختارة غير مفعّلة للتشغيل حاليًا.");

        }



        var hasActiveVendor = await context.VendorBranches

            .AsNoTracking()

            .AnyAsync(

                branch =>

                    branch.IsActive

                    && branch.Vendor.Status == VendorStatus.Active

                    && branch.Vendor.AcceptOrders

                    && branch.Vendor.LockedAtUtc == null

                    && (

                        branch.Region == normalizedRegion

                        || branch.Vendor.Region == normalizedRegion),

                cancellationToken);



        if (!hasActiveVendor)

        {

            throw new BusinessRuleException(

                "DRIVER_REGION_HAS_NO_ACTIVE_VENDOR",

                "المنطقة المختارة ما فيها متاجر متاحة حاليًا.");

        }

    }



    public static Task<bool> IsOperationalRegionAsync(

        IApplicationDbContext context,

        string? regionCode,

        CancellationToken cancellationToken)

    {

        var normalizedRegion = NormalizeCode(regionCode);

        if (normalizedRegion.Length == 0)

        {

            return Task.FromResult(false);

        }



        return context.SaudiRegions

            .AsNoTracking()

            .AnyAsync(

                region => region.Code == normalizedRegion && region.IsOperational,

                cancellationToken);

    }



    public static Task<bool> IsOperationalCityAsync(

        IApplicationDbContext context,

        string? regionCode,

        string? cityCode,

        CancellationToken cancellationToken)

    {

        var normalizedRegion = NormalizeCode(regionCode);

        var normalizedCity = NormalizeCode(cityCode);

        if (normalizedRegion.Length == 0 || normalizedCity.Length == 0)

        {

            return Task.FromResult(false);

        }



        return context.SaudiCities

            .AsNoTracking()

            .AnyAsync(

                city =>

                    city.Code == normalizedCity

                    && city.IsOperational

                    && city.Region.Code == normalizedRegion

                    && city.Region.IsOperational,

                cancellationToken);

    }



    public static async Task<IReadOnlyList<string>> GetOperationalRegionCodesAsync(

        IApplicationDbContext context,

        CancellationToken cancellationToken)

    {

        return await context.SaudiRegions

            .AsNoTracking()

            .Where(region => region.IsOperational)

            .OrderBy(region => region.SortOrder)

            .ThenBy(region => region.NameEn)

            .Select(region => region.Code)

            .ToListAsync(cancellationToken);

    }



    public static async Task<IReadOnlyList<string>> GetOperationalCityCodesAsync(

        IApplicationDbContext context,

        string? regionCode,

        CancellationToken cancellationToken)

    {

        var normalizedRegion = NormalizeCode(regionCode);

        if (normalizedRegion.Length == 0)

        {

            return [];

        }



        return await context.SaudiCities

            .AsNoTracking()

            .Where(city =>

                city.Region.Code == normalizedRegion

                && city.IsOperational

                && city.Region.IsOperational)

            .OrderBy(city => city.SortOrder)

            .ThenBy(city => city.NameEn)

            .Select(city => city.Code)

            .ToListAsync(cancellationToken);

    }



    private static string NormalizeCode(string? value) =>

        (value ?? string.Empty).Trim().ToUpperInvariant();

    public static async Task<bool> IsOperationalAddressCityAsync(
        IApplicationDbContext context,
        string? cityName,
        CancellationToken cancellationToken)
    {
        var city = await ResolveCityByNameAsync(context, cityName, cancellationToken);
        if (city is null)
        {
            return false;
        }

        return city.IsOperational && city.Region.IsOperational;
    }

    public static async Task<bool> IsOperationalBranchAsync(
        IApplicationDbContext context,
        string? regionCode,
        string? cityName,
        CancellationToken cancellationToken)
    {
        var normalizedRegion = NormalizeCode(regionCode);
        if (normalizedRegion.Length == 0)
        {
            return false;
        }

        if (!await IsOperationalRegionAsync(context, normalizedRegion, cancellationToken))
        {
            return false;
        }

        var city = await ResolveCityByNameAsync(context, cityName, cancellationToken);
        if (city is null)
        {
            return false;
        }

        return city.Region.Code == normalizedRegion && city.IsOperational;
    }

    private static async Task<SaudiCity?> ResolveCityByNameAsync(
        IApplicationDbContext context,
        string? cityName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return null;
        }

        var trimmed = cityName.Trim();
        var normalizedCity = DeliveryCityMatcher.Normalize(trimmed);

        // Load locally: DeliveryCityMatcher.Normalize cannot be translated by SQL Server
        // and was throwing 500 on checkout/summary for Arabic city names like "الظهران".
        var cities = await context.SaudiCities
            .AsNoTracking()
            .Include(city => city.Region)
            .ToListAsync(cancellationToken);

        return cities.FirstOrDefault(city =>
            string.Equals(city.Code, trimmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(city.NameAr, trimmed, StringComparison.Ordinal)
            || string.Equals(city.NameEn, trimmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(DeliveryCityMatcher.Normalize(city.NameAr), normalizedCity, StringComparison.OrdinalIgnoreCase)
            || string.Equals(DeliveryCityMatcher.Normalize(city.NameEn), normalizedCity, StringComparison.OrdinalIgnoreCase));
    }
}

