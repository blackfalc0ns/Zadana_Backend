using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Finances.Entities;
using Zadana.Domain.Modules.Geography.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Infrastructure.Modules.Delivery.Services;

public class DeliveryPricingService : IDeliveryPricingService
{
    private readonly IApplicationDbContext _context;
    private readonly IDriverCommitmentPolicyService _driverCommitmentPolicyService;
    private readonly DeliveryPricingCacheService _pricingCache;

    public DeliveryPricingService(
        IApplicationDbContext context,
        IDriverCommitmentPolicyService driverCommitmentPolicyService,
        DeliveryPricingCacheService pricingCache)
    {
        _context = context;
        _driverCommitmentPolicyService = driverCommitmentPolicyService;
        _pricingCache = pricingCache;
    }

    public async Task<DeliveryPriceQuote> QuoteAsync(
        Guid vendorBranchId,
        Guid customerAddressId,
        CancellationToken cancellationToken = default,
        decimal? orderSubtotal = null)
    {
        var branch = await _context.VendorBranches
            .Include(item => item.Vendor)
            .FirstOrDefaultAsync(item => item.Id == vendorBranchId, cancellationToken)
            ?? throw new NotFoundException("VendorBranch", vendorBranchId);

        var address = await _context.CustomerAddresses
            .FirstOrDefaultAsync(item => item.Id == customerAddressId, cancellationToken)
            ?? throw new NotFoundException("CustomerAddress", customerAddressId);

        var pricingRules = await _pricingCache.GetPricingRulesAsync(cancellationToken);

        var zoneFinanceSettings = await _pricingCache.GetZoneFinanceSettingsAsync(cancellationToken);

        var cityPricingSettings = await _pricingCache.GetCityPricingSettingsAsync(cancellationToken);

        var regionPricingSettings = await _pricingCache.GetRegionPricingSettingsAsync(cancellationToken);

        var deliveryDefaults = await _pricingCache.GetDeliveryDefaultsAsync(cancellationToken);

        var cities = await _pricingCache.GetCitiesAsync(cancellationToken);

        var zones = pricingRules
            .Where(rule => rule.DeliveryZone != null)
            .Select(rule => rule.DeliveryZone!)
            .DistinctBy(zone => zone.Id)
            .ToArray();

        var vendorPickupCity = FirstNonBlank(branch.City, branch.Vendor.City);
        var vendorPickupRegion = FirstNonBlank(branch.Region, branch.Vendor.Region);
        if (string.IsNullOrWhiteSpace(vendorPickupCity))
        {
            throw new BusinessRuleException(
                "DELIVERY_PICKUP_CITY_REQUIRED",
                "Vendor branch city is required for delivery pricing.");
        }

        var vendorCity = ResolveCity(cities, vendorPickupCity, vendorPickupRegion);
        var customerCity = ResolveCity(cities, address.City, null);
        var hasBranchCoordinates = !(branch.Latitude == 0m && branch.Longitude == 0m);

        var vendorZone = hasBranchCoordinates
            ? ResolveLocationZone(zones, branch.Latitude, branch.Longitude, vendorPickupCity)
            : ResolveZoneByCity(zones, vendorPickupCity);
        var customerPoint = ResolveCustomerPoint(address, customerCity, zones);
        var customerZone = customerPoint.Zone;
        var vendorPickupPoint = ResolveVendorPickupPoint(branch, vendorCity, vendorZone);

        var driverOrigin = await ResolveDriverOriginAsync(
            vendorPickupPoint.Latitude,
            vendorPickupPoint.Longitude,
            zones,
            vendorZone,
            vendorCity,
            vendorPickupCity,
            cancellationToken);

        var vendorLegSettings = ResolveLegPricingSettings(
            pricingRules,
            zoneFinanceSettings,
            cityPricingSettings,
            regionPricingSettings,
            deliveryDefaults,
            vendorZone,
            vendorCity);
        var customerLegSettings = ResolveLegPricingSettings(
            pricingRules,
            zoneFinanceSettings,
            cityPricingSettings,
            regionPricingSettings,
            deliveryDefaults,
            customerZone,
            customerCity);

        var driverToVendorDistanceKm = DeliveryDispatchScoring.ApproximateDistanceKm(
            driverOrigin.Latitude,
            driverOrigin.Longitude,
            vendorPickupPoint.Latitude,
            vendorPickupPoint.Longitude);

        var vendorToCustomerDistanceKm = DeliveryDispatchScoring.ApproximateDistanceKm(
            vendorPickupPoint.Latitude,
            vendorPickupPoint.Longitude,
            customerPoint.Latitude,
            customerPoint.Longitude);

        var driverToVendorLeg = driverOrigin.Mode == "live"
            ? QuoteLeg(vendorLegSettings, driverToVendorDistanceKm)
            : QuotedLeg.Empty;
        var vendorToCustomerLeg = QuoteLeg(customerLegSettings, vendorToCustomerDistanceKm);

        var baseFee = decimal.Round(driverToVendorLeg.BaseFee + vendorToCustomerLeg.BaseFee, 2, MidpointRounding.AwayFromZero);
        var distanceFee = decimal.Round(driverToVendorLeg.DistanceFee + vendorToCustomerLeg.DistanceFee, 2, MidpointRounding.AwayFromZero);
        var surgeFee = decimal.Round(driverToVendorLeg.SurgeFee + vendorToCustomerLeg.SurgeFee, 2, MidpointRounding.AwayFromZero);
        var totalFee = decimal.Round(baseFee + distanceFee + surgeFee, 2, MidpointRounding.AwayFromZero);
        var totalDistanceKm = decimal.Round(driverToVendorDistanceKm + vendorToCustomerDistanceKm, 2, MidpointRounding.AwayFromZero);
        var pricingMode = driverOrigin.Mode;
        var totalClamp = ResolveTotalClamp(deliveryDefaults);
        totalFee = ApplyTotalClamp(totalFee, totalClamp.MinTotalDeliveryFee, totalClamp.MaxTotalDeliveryFee);
        ApplyTotalAdjustment(totalFee, ref baseFee, ref distanceFee, ref surgeFee);
        var hasAnomalyWarning =
            (totalClamp.MaxQuotedDistanceKm > 0m && totalDistanceKm > totalClamp.MaxQuotedDistanceKm) ||
            ShouldWarnForSubtotalRatio(totalFee, orderSubtotal, totalClamp.WarningSubtotalRatioThreshold);
        var quoteLockedAtUtc = DateTime.UtcNow;

        return new DeliveryPriceQuote(
            baseFee,
            distanceFee,
            surgeFee,
            totalFee,
            totalDistanceKm,
            pricingMode,
            $"{vendorLegSettings.Label} -> {customerLegSettings.Label}",
            decimal.Round(driverToVendorDistanceKm, 2, MidpointRounding.AwayFromZero),
            decimal.Round(vendorToCustomerDistanceKm, 2, MidpointRounding.AwayFromZero),
            driverToVendorLeg.TotalFee,
            vendorToCustomerLeg.TotalFee,
            vendorLegSettings.Source,
            customerLegSettings.Source,
            driverOrigin.Mode == "estimated",
            driverOrigin.Source,
            driverOrigin.DriverId,
            driverOrigin.Mode == "live" ? "live_locked" : "estimated_locked",
            quoteLockedAtUtc,
            2,
            hasAnomalyWarning);
    }

    private async Task<PricingOriginPoint> ResolveDriverOriginAsync(
        decimal pickupLatitude,
        decimal pickupLongitude,
        IReadOnlyCollection<DeliveryZone> zones,
        DeliveryZone? vendorZone,
        SaudiCity? vendorCity,
        string pickupCity,
        CancellationToken cancellationToken)
    {
        var busyDriverIds = await _context.DeliveryAssignments
            .Where(item =>
                item.DriverId.HasValue &&
                item.Status != AssignmentStatus.Delivered &&
                item.Status != AssignmentStatus.Failed &&
                item.Status != AssignmentStatus.Cancelled &&
                item.Status != AssignmentStatus.Rejected &&
                item.Status != AssignmentStatus.Returned &&
                !DeliveryActiveAssignmentRules.TerminalOrderStatuses.Contains(item.Order.Status))
            .Select(item => item.DriverId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var eligibleDrivers = (await _context.Drivers
                .AsNoTracking()
                .Where(driver => !busyDriverIds.Contains(driver.Id))
                .ToListAsync(cancellationToken))
            .Where(driver => driver.CanReceiveNewOffers)
            .ToList();

        if (eligibleDrivers.Any())
        {
            var driverIds = eligibleDrivers.Select(driver => driver.Id).ToList();
            await _driverCommitmentPolicyService.ApplyOperationalEnforcementAsync(driverIds, cancellationToken);
            var commitment = await _driverCommitmentPolicyService.GetDriverSummariesAsync(driverIds, cancellationToken);

            eligibleDrivers = eligibleDrivers
                .Where(driver =>
                    commitment.TryGetValue(driver.Id, out var summary) &&
                    summary.CanReceiveOffers)
                .ToList();

            if (eligibleDrivers.Any())
            {
                driverIds = eligibleDrivers.Select(driver => driver.Id).ToList();

                var latestLocations = await _context.DriverLocations
                    .AsNoTracking()
                    .Where(location => driverIds.Contains(location.DriverId))
                    .GroupBy(location => location.DriverId)
                    .Select(group => group.OrderByDescending(location => location.RecordedAtUtc).First())
                    .ToListAsync(cancellationToken);

                var liveLocation = latestLocations
                    .Where(location =>
                        (DateTime.UtcNow - location.RecordedAtUtc) <= DeliveryDispatchScoring.GpsFreshnessThreshold &&
                        (!location.AccuracyMeters.HasValue || location.AccuracyMeters.Value <= DeliveryDispatchScoring.LowConfidenceAccuracyMeters) &&
                        DeliveryPickupAreaMatcher.DriverMatchesPickup(
                            location.Latitude,
                            location.Longitude,
                            pickupLatitude,
                            pickupLongitude,
                            gpsFresh: true))
                    .OrderBy(location => DeliveryDispatchScoring.ApproximateDistanceKm(
                        location.Latitude,
                        location.Longitude,
                        pickupLatitude,
                        pickupLongitude))
                    .FirstOrDefault();

                if (liveLocation is not null)
                {
                    return new PricingOriginPoint(
                        liveLocation.Latitude,
                        liveLocation.Longitude,
                        "live",
                        "live_driver",
                        liveLocation.DriverId);
                }
            }
        }

        if (vendorZone is not null)
        {
            return new PricingOriginPoint(vendorZone.CenterLat, vendorZone.CenterLng, "estimated", "vendor_zone_center", null);
        }

        if (vendorCity is not null)
        {
            return new PricingOriginPoint((decimal)vendorCity.Latitude, (decimal)vendorCity.Longitude, "estimated", "vendor_city_center", null);
        }

        return new PricingOriginPoint(pickupLatitude, pickupLongitude, "estimated", "vendor_branch_fallback", null);
    }

    private static PricingOriginPoint ResolveVendorPickupPoint(
        VendorBranch branch,
        SaudiCity? vendorCity,
        DeliveryZone? vendorZone)
    {
        if (!(branch.Latitude == 0m && branch.Longitude == 0m))
        {
            return new PricingOriginPoint(branch.Latitude, branch.Longitude, "branch", "branch_coordinates", null);
        }

        if (vendorZone is not null)
        {
            return new PricingOriginPoint(vendorZone.CenterLat, vendorZone.CenterLng, "estimated", "vendor_zone_center", null);
        }

        if (vendorCity is not null)
        {
            return new PricingOriginPoint((decimal)vendorCity.Latitude, (decimal)vendorCity.Longitude, "estimated", "vendor_city_center", null);
        }

        throw new BusinessRuleException("DELIVERY_PRICING_UNAVAILABLE", "Vendor pickup coordinates are required for delivery pricing.");
    }

    private static CustomerPricingPoint ResolveCustomerPoint(
        CustomerAddress address,
        SaudiCity? customerCity,
        IReadOnlyCollection<DeliveryZone> zones)
    {
        var hasCoordinates = address.Latitude.HasValue
            && address.Longitude.HasValue
            && !(address.Latitude.Value == 0m && address.Longitude.Value == 0m);

        var latitude = address.Latitude ?? 0m;
        var longitude = address.Longitude ?? 0m;

        if (hasCoordinates
            && !IsInsideAnyZone(zones, latitude, longitude)
            && IsInsideAnyZone(zones, longitude, latitude))
        {
            (latitude, longitude) = (longitude, latitude);
        }

        var zone = hasCoordinates
            ? ResolveLocationZone(zones, latitude, longitude, address.City)
            : ResolveZoneByCity(zones, address.City);

        if (hasCoordinates)
        {
            return new CustomerPricingPoint(latitude, longitude, zone);
        }

        if (zone is not null)
        {
            return new CustomerPricingPoint(zone.CenterLat, zone.CenterLng, zone);
        }

        if (customerCity is not null)
        {
            return new CustomerPricingPoint((decimal)customerCity.Latitude, (decimal)customerCity.Longitude, null);
        }

        throw new BusinessRuleException(
            "DELIVERY_PRICING_UNAVAILABLE",
            "Delivery pricing is not configured for the selected address.");
    }

    private static LegPricingSettings ResolveLegPricingSettings(
        IReadOnlyCollection<DeliveryPricingRule> pricingRules,
        IReadOnlyDictionary<Guid, ZoneFinanceSettings> zoneFinanceSettings,
        IReadOnlyCollection<CityDeliveryPricingSettings> cityPricingSettings,
        IReadOnlyCollection<RegionDeliveryPricingSettings> regionPricingSettings,
        DeliveryPricingDefaults? deliveryDefaults,
        DeliveryZone? zone,
        SaudiCity? city)
    {
        if (zone is not null)
        {
            var zoneRule = pricingRules.FirstOrDefault(item => item.DeliveryZoneId == zone.Id);
            if (zoneRule is not null)
            {
                zoneFinanceSettings.TryGetValue(zone.Id, out var zoneFinance);
                return new LegPricingSettings(
                    zoneRule.BaseFee,
                    zoneRule.IncludedKm,
                    zoneRule.PerKmFee,
                    zoneRule.MinFee,
                    zoneRule.MaxFee,
                    zoneRule.SurgeWindows.ToArray(),
                    zoneFinance?.VatPercent ?? 15m,
                    zoneFinance?.CodFeeType ?? "flat",
                    zoneFinance?.CodFlatFee ?? 0m,
                    zoneFinance?.CodPercent ?? 0m,
                    zoneFinance?.IsVatActive ?? true,
                    zoneFinance?.IsCodFeeActive ?? false,
                    "zone",
                    zoneRule.Name);
            }
        }

        if (city is not null)
        {
            var citySettings = cityPricingSettings.FirstOrDefault(item => item.SaudiCityId == city.Id && item.IsPricingActive);
            if (citySettings is not null)
            {
                return new LegPricingSettings(
                    citySettings.BaseDeliveryFee,
                    citySettings.IncludedKm,
                    citySettings.ExtraKmFee,
                    citySettings.MinDeliveryFee,
                    citySettings.MaxDeliveryFee,
                    [],
                    citySettings.VatPercent,
                    citySettings.CodFeeType,
                    citySettings.CodFlatFee,
                    citySettings.CodPercent,
                    citySettings.IsVatActive,
                    citySettings.IsCodFeeActive,
                    "city",
                    city.NameEn);
            }

            var regionSettings = regionPricingSettings.FirstOrDefault(item => item.SaudiRegionId == city.RegionId && item.IsPricingActive);
            if (regionSettings is not null)
            {
                return new LegPricingSettings(
                    regionSettings.BaseDeliveryFee,
                    regionSettings.IncludedKm,
                    regionSettings.ExtraKmFee,
                    regionSettings.MinDeliveryFee,
                    regionSettings.MaxDeliveryFee,
                    [],
                    regionSettings.VatPercent,
                    regionSettings.CodFeeType,
                    regionSettings.CodFlatFee,
                    regionSettings.CodPercent,
                    regionSettings.IsVatActive,
                    regionSettings.IsCodFeeActive,
                    "region",
                    city.Region?.NameEn ?? city.NameEn);
            }
        }

        if (deliveryDefaults is not null && deliveryDefaults.IsPricingActive)
        {
            return new LegPricingSettings(
                deliveryDefaults.BaseDeliveryFee,
                deliveryDefaults.IncludedKm,
                deliveryDefaults.ExtraKmFee,
                deliveryDefaults.MinDeliveryFee,
                deliveryDefaults.MaxDeliveryFee,
                [],
                deliveryDefaults.VatPercent,
                deliveryDefaults.CodFeeType,
                deliveryDefaults.CodFlatFee,
                deliveryDefaults.CodPercent,
                deliveryDefaults.IsVatActive,
                deliveryDefaults.IsCodFeeActive,
                "global_fallback",
                "Global default");
        }

        throw new BusinessRuleException(
            "DELIVERY_PRICING_UNAVAILABLE",
            "No active delivery pricing settings are configured.");
    }

    private static QuotedLeg QuoteLeg(LegPricingSettings settings, decimal distanceKm)
    {
        var baseFee = settings.BaseDeliveryFee;
        var extraDistanceFee = Math.Max(0m, decimal.Round(distanceKm - settings.IncludedKm, 2, MidpointRounding.AwayFromZero)) * settings.ExtraKmFee;
        extraDistanceFee = decimal.Round(extraDistanceFee, 2, MidpointRounding.AwayFromZero);

        var activeMultiplier = ResolveActiveSurgeMultiplier(settings.SurgeWindows);
        var surgeFee = activeMultiplier > 1m
            ? decimal.Round((baseFee + extraDistanceFee) * (activeMultiplier - 1m), 2, MidpointRounding.AwayFromZero)
            : 0m;

        ApplyClamp(settings.MinDeliveryFee, settings.MaxDeliveryFee, ref baseFee, ref extraDistanceFee, ref surgeFee);

        return new QuotedLeg(
            decimal.Round(baseFee + extraDistanceFee + surgeFee, 2, MidpointRounding.AwayFromZero),
            baseFee,
            extraDistanceFee,
            surgeFee);
    }

    private static DeliveryZone? ResolveLocationZone(
        IReadOnlyCollection<DeliveryZone> zones,
        decimal latitude,
        decimal longitude,
        string? fallbackCity)
    {
        var containingZone = DeliveryDispatchScoring.ResolveContainingZone(zones, latitude, longitude);
        if (containingZone is not null)
        {
            return containingZone;
        }

        var nearestCityZone = ResolveNearestCityZone(zones, fallbackCity, latitude, longitude);
        if (nearestCityZone is not null)
        {
            return nearestCityZone;
        }

        return DeliveryDispatchScoring.ResolveNearestZone(zones, latitude, longitude);
    }

    private static DeliveryZone? ResolveZoneByCity(
        IReadOnlyCollection<DeliveryZone> zones,
        string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return null;
        }

        return zones.FirstOrDefault(zone => zone.IsActive && CityMatches(zone.City, city));
    }

    private static SaudiCity? ResolveCity(
        IReadOnlyCollection<SaudiCity> cities,
        string? cityValue,
        string? regionValue)
    {
        var normalizedCity = DeliveryCityMatcher.Normalize(cityValue);
        var normalizedRegion = NormalizeRegion(regionValue);

        return cities.FirstOrDefault(city =>
                !string.IsNullOrWhiteSpace(normalizedRegion) &&
                city.Region != null &&
                string.Equals(city.Region.Code, normalizedRegion, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(city.Code, cityValue?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 CityMatches(city.NameAr, cityValue) ||
                 CityMatches(city.NameEn, cityValue)))
            ?? cities.FirstOrDefault(city =>
                string.Equals(city.Code, cityValue?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                CityMatches(city.NameAr, normalizedCity) ||
                CityMatches(city.NameEn, normalizedCity));
    }

    private static string? NormalizeRegion(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return null;
        }

        return region.Trim().ToUpperInvariant();
    }

    private static DeliveryZone? ResolveNearestCityZone(
        IReadOnlyCollection<DeliveryZone> zones,
        string? city,
        decimal latitude,
        decimal longitude)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return null;
        }

        return zones
            .Where(zone => zone.IsActive && CityMatches(zone.City, city))
            .OrderBy(zone => DeliveryDispatchScoring.ApproximateDistanceKm(zone.CenterLat, zone.CenterLng, latitude, longitude))
            .FirstOrDefault();
    }

    private static bool IsInsideAnyZone(IReadOnlyCollection<DeliveryZone> zones, decimal latitude, decimal longitude) =>
        zones.Any(zone => zone.IsActive && DeliveryDispatchScoring.IsPointWithinZone(zone, latitude, longitude));

    private static bool CityMatches(string? left, string? right)
        => DeliveryCityMatcher.Matches(left, right);

    private static string? NormalizeCity(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return null;
        }

        var normalized = city.Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);

        return normalized switch
        {
            // المنطقة الشرقية (Eastern Province)
            "الدمام" or "دمام" or "dammam" => "dammam",
            "الخبر" or "خبر" or "khobar" or "alkhobar" => "khobar",
            "الظهران" or "ظهران" or "dhahran" => "dhahran",
            "الجبيل" or "جبيل" or "jubail" or "jubel" => "jubail",
            "القطيف" or "قطيف" or "qatif" or "alqatif" => "qatif",
            "الاحساء" or "الأحساء" or "احساء" or "أحساء" or "الهفوف" or "هفوف" or "ahsa" or "alahsa" or "alhasa" or "hofuf" or "alhofuf" => "ahsa",
            "حفرالباطن" or "حفر" or "hafr" or "hafralbatn" or "hafr_al_batin" or "hafralbatin" => "hafralbatin",
            "رأستنورة" or "راستنورة" or "رأستنوره" or "rastanura" or "rastanorah" => "rastanura",
            "الخفجي" or "خفجي" or "khafji" or "alkhafji" => "khafji",
            "بقيق" or "buqayq" or "abqaiq" => "abqaiq",
            "النعيرية" or "نعيرية" or "nairyah" or "nuayriyah" => "nairyah",
            "سيهات" or "saihat" or "sayhat" => "saihat",
            "تاروت" or "tarut" or "tarout" => "tarut",
            "صفوى" or "صفوا" or "safwa" => "safwa",
            "العوامية" or "عوامية" or "awamiyah" => "awamiyah",
            "رحيمة" or "rahima" or "rahimah" => "rahima",

            // مدن أخرى (للتوافق مع البيانات القديمة)
            "الرياض" or "رياض" or "riyadh" => "riyadh",
            "جدة" or "جده" or "jeddah" => "jeddah",
            _ => normalized
        };
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static readonly TimeZoneInfo SaudiTimeZone = ResolveSaudiTimeZone();

    private static TimeZoneInfo ResolveSaudiTimeZone()
    {
        foreach (var id in new[] { "Asia/Riyadh", "Arab Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Saudi Arabia Standard Time",
            TimeSpan.FromHours(3),
            "Saudi Arabia Standard Time",
            "Saudi Arabia Standard Time");
    }

    private static decimal ResolveActiveSurgeMultiplier(IReadOnlyCollection<DeliveryPricingSurgeWindow> windows)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SaudiTimeZone).TimeOfDay;

        foreach (var window in windows.Where(item => item.IsActive).OrderBy(item => item.StartLocalTime))
        {
            var isActive = window.StartLocalTime <= window.EndLocalTime
                ? now >= window.StartLocalTime && now <= window.EndLocalTime
                : now >= window.StartLocalTime || now <= window.EndLocalTime;

            if (isActive)
            {
                return Math.Max(1m, window.Multiplier);
            }
        }

        return 1m;
    }

    private static void ApplyClamp(
        decimal minFee,
        decimal maxFee,
        ref decimal baseFee,
        ref decimal distanceFee,
        ref decimal surgeFee)
    {
        var total = baseFee + distanceFee + surgeFee;

        if (total < minFee)
        {
            distanceFee += minFee - total;
            return;
        }

        if (maxFee <= 0 || total <= maxFee)
        {
            return;
        }

        var overflow = total - maxFee;

        var distanceReduction = Math.Min(distanceFee, overflow);
        distanceFee -= distanceReduction;
        overflow -= distanceReduction;

        if (overflow <= 0)
        {
            return;
        }

        var surgeReduction = Math.Min(surgeFee, overflow);
        surgeFee -= surgeReduction;
        overflow -= surgeReduction;

        if (overflow <= 0)
        {
            return;
        }

        baseFee = Math.Max(0m, baseFee - overflow);
    }

    private static TotalClampSettings ResolveTotalClamp(DeliveryPricingDefaults? defaults) =>
        defaults is null
            ? new TotalClampSettings(0m, 0m, 0m, 0m)
            : new TotalClampSettings(
                defaults.MinTotalDeliveryFee,
                defaults.MaxTotalDeliveryFee,
                defaults.MaxQuotedDistanceKm,
                defaults.WarningSubtotalRatioThreshold);

    private static decimal ApplyTotalClamp(decimal totalFee, decimal minTotalDeliveryFee, decimal maxTotalDeliveryFee)
    {
        var result = totalFee;
        if (minTotalDeliveryFee > 0m && result < minTotalDeliveryFee)
        {
            result = minTotalDeliveryFee;
        }

        if (maxTotalDeliveryFee > 0m && result > maxTotalDeliveryFee)
        {
            result = maxTotalDeliveryFee;
        }

        return decimal.Round(result, 2, MidpointRounding.AwayFromZero);
    }

    private static void ApplyTotalAdjustment(
        decimal targetTotal,
        ref decimal baseFee,
        ref decimal distanceFee,
        ref decimal surgeFee)
    {
        var currentTotal = decimal.Round(baseFee + distanceFee + surgeFee, 2, MidpointRounding.AwayFromZero);
        var delta = decimal.Round(targetTotal - currentTotal, 2, MidpointRounding.AwayFromZero);
        if (delta == 0m)
        {
            return;
        }

        if (delta > 0m)
        {
            distanceFee += delta;
            return;
        }

        var remainingReduction = Math.Abs(delta);
        var distanceReduction = Math.Min(distanceFee, remainingReduction);
        distanceFee -= distanceReduction;
        remainingReduction -= distanceReduction;

        var surgeReduction = Math.Min(surgeFee, remainingReduction);
        surgeFee -= surgeReduction;
        remainingReduction -= surgeReduction;

        if (remainingReduction > 0m)
        {
            baseFee = Math.Max(0m, baseFee - remainingReduction);
        }
    }

    private static bool ShouldWarnForSubtotalRatio(
        decimal totalFee,
        decimal? orderSubtotal,
        decimal warningSubtotalRatioThreshold)
    {
        return warningSubtotalRatioThreshold > 0m &&
            warningSubtotalRatioThreshold < 1m &&
            orderSubtotal.HasValue &&
            orderSubtotal.Value > 0m &&
            totalFee > orderSubtotal.Value * warningSubtotalRatioThreshold;
    }

    private sealed record PricingOriginPoint(decimal Latitude, decimal Longitude, string Mode, string Source, Guid? DriverId);

    private sealed record CustomerPricingPoint(decimal Latitude, decimal Longitude, DeliveryZone? Zone);

    private sealed record LegPricingSettings(
        decimal BaseDeliveryFee,
        decimal IncludedKm,
        decimal ExtraKmFee,
        decimal MinDeliveryFee,
        decimal MaxDeliveryFee,
        IReadOnlyCollection<DeliveryPricingSurgeWindow> SurgeWindows,
        decimal VatPercent,
        string CodFeeType,
        decimal CodFlatFee,
        decimal CodPercent,
        bool IsVatActive,
        bool IsCodFeeActive,
        string Source,
        string Label);

    private sealed record QuotedLeg(decimal TotalFee, decimal BaseFee, decimal DistanceFee, decimal SurgeFee)
    {
        public static QuotedLeg Empty { get; } = new(0m, 0m, 0m, 0m);
    }

    private sealed record TotalClampSettings(
        decimal MinTotalDeliveryFee,
        decimal MaxTotalDeliveryFee,
        decimal MaxQuotedDistanceKm,
        decimal WarningSubtotalRatioThreshold);
}
