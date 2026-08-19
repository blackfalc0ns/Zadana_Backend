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

        var hasActiveVendor = await context.VendorBranches
            .AsNoTracking()
            .AnyAsync(
                branch =>
                    branch.IsActive
                    && branch.Vendor.Status == VendorStatus.Active
                    && branch.Vendor.AcceptOrders
                    && branch.Vendor.LockedAtUtc == null
                    && (
                        branch.Region == EasternRegionCode
                        || branch.Vendor.Region == EasternRegionCode
                        || branch.City == "DAMMAM"
                        || branch.City == "KHOBAR"
                        || branch.City == "DHAHRAN"
                        || branch.City == "الدمام"
                        || branch.City == "الخبر"
                        || branch.City == "الظهران"
                        || branch.City == "Dammam"
                        || branch.City == "Al Khobar"
                        || branch.City == "Dhahran"),
                cancellationToken);

        if (!hasActiveVendor)
        {
            throw new BusinessRuleException(
                "DRIVER_REGION_HAS_NO_ACTIVE_VENDOR",
                "المنطقة الشرقية ما فيها متاجر متاحة حاليًا.");
        }
    }

    private static string NormalizeCode(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();
}
