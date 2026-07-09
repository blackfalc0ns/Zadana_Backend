using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Geography.Support;

public static class OperationalGeographyScope
{
    public const string EasternRegionCode = "EASTERN";

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

        if (normalizedRegion != EasternRegionCode)
        {
            throw new BusinessRuleException(
                "UNSUPPORTED_OPERATIONAL_REGION",
                "حاليًا التشغيل متاح في المنطقة الشرقية بس.");
        }

        var normalizedCity = NormalizeCode(cityCode);
        if (normalizedCity.Length == 0)
        {
            throw new BusinessRuleException(
                "SERVICE_CITY_REQUIRED",
                "لازم تختار مدينة التشغيل.");
        }

        var cityExistsInOperationalRegion = await context.SaudiCities
            .AsNoTracking()
            .AnyAsync(
                city => city.Code == normalizedCity && city.Region.Code == EasternRegionCode,
                cancellationToken);

        if (!cityExistsInOperationalRegion)
        {
            throw new BusinessRuleException(
                "UNSUPPORTED_OPERATIONAL_CITY",
                "لازم تختار مدينة من مدن المنطقة الشرقية.");
        }
    }

    public static async Task EnsureDriverServiceAreaAsync(
        IApplicationDbContext context,
        string? regionCode,
        string? cityCode,
        CancellationToken cancellationToken)
    {
        await EnsureOperationalRegionCityAsync(context, regionCode, cityCode, cancellationToken);

        var normalizedCity = NormalizeCode(cityCode);
        var city = await context.SaudiCities
            .AsNoTracking()
            .Where(item => item.Code == normalizedCity && item.Region.Code == EasternRegionCode)
            .Select(item => new OperationalCityLookup(item.Code, item.NameAr, item.NameEn))
            .FirstAsync(cancellationToken);

        var hasActiveVendor = await context.VendorBranches
            .AsNoTracking()
            .AnyAsync(
                branch =>
                    branch.IsActive
                    && branch.Vendor.Status == VendorStatus.Active
                    && branch.Vendor.AcceptOrders
                    && branch.Vendor.LockedAtUtc == null
                    && (
                        branch.City == city.Code
                        || branch.City == city.NameAr
                        || branch.City == city.NameEn
                        || (
                            branch.City == string.Empty
                            && (
                                branch.Vendor.City == city.Code
                                || branch.Vendor.City == city.NameAr
                                || branch.Vendor.City == city.NameEn))),
                cancellationToken);

        if (!hasActiveVendor)
        {
            throw new BusinessRuleException(
                "DRIVER_CITY_HAS_NO_ACTIVE_VENDOR",
                "المدينة هذي ما فيها متاجر متاحة حاليًا. اختر مدينة ثانية من المدن المدعومة.");
        }
    }

    private static string NormalizeCode(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();

    private sealed record OperationalCityLookup(string Code, string NameAr, string NameEn);
}
