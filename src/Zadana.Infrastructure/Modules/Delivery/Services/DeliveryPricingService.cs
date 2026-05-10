using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Infrastructure.Modules.Delivery.Services;

public class DeliveryPricingService : IDeliveryPricingService
{
    private readonly IApplicationDbContext _context;

    public DeliveryPricingService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DeliveryPriceQuote> QuoteAsync(
        Guid vendorBranchId,
        Guid customerAddressId,
        CancellationToken cancellationToken = default)
    {
        var branch = await _context.VendorBranches
            .Include(item => item.Vendor)
            .FirstOrDefaultAsync(item => item.Id == vendorBranchId, cancellationToken)
            ?? throw new NotFoundException("VendorBranch", vendorBranchId);

        var address = await _context.CustomerAddresses
            .FirstOrDefaultAsync(item => item.Id == customerAddressId, cancellationToken)
            ?? throw new NotFoundException("CustomerAddress", customerAddressId);

        var rules = await _context.DeliveryPricingRules
            .Include(item => item.DeliveryZone)
            .Include(item => item.SurgeWindows)
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.DeliveryZoneId != null)
            .ToListAsync(cancellationToken);

        var zones = rules
            .Where(rule => rule.DeliveryZone != null)
            .Select(rule => rule.DeliveryZone!)
            .DistinctBy(zone => zone.Id)
            .ToArray();

        var hasExactCoordinates = address.Latitude.HasValue
            && address.Longitude.HasValue
            && !(address.Latitude.Value == 0m && address.Longitude.Value == 0m);
        var latitude = address.Latitude;
        var longitude = address.Longitude;

        if (hasExactCoordinates
            && !IsInsideAnyZone(zones, latitude!.Value, longitude!.Value)
            && IsInsideAnyZone(zones, longitude!.Value, latitude!.Value))
        {
            (latitude, longitude) = (longitude, latitude);
        }

        var distanceKm = hasExactCoordinates
            ? DeliveryDispatchScoring.ApproximateDistanceKm(
                branch.Latitude,
                branch.Longitude,
                latitude!.Value,
                longitude!.Value)
            : 0m;

        var addressZone = hasExactCoordinates
            ? DeliveryDispatchScoring.ResolveContainingZone(
                zones,
                latitude!.Value,
                longitude!.Value)
            : null;

        if (hasExactCoordinates && addressZone is null)
        {
            addressZone = ResolveNearestCityZone(zones, address.City, latitude!.Value, longitude!.Value)
                ?? ResolveNearestCityZone(zones, branch.Vendor.City, latitude!.Value, longitude!.Value);
        }

        var rule = ResolveRule(rules, addressZone?.Id, address.City, branch.Vendor.City);
        if (rule is null)
        {
            throw new BusinessRuleException(
                "DELIVERY_PRICING_UNAVAILABLE",
                "Delivery pricing is not configured for the selected address.");
        }

        var pricingMode = hasExactCoordinates ? "exact-distance" : "zone-fallback";
        var baseFee = rule.BaseFee;
        var distanceFee = hasExactCoordinates
            ? Math.Max(0m, decimal.Round(distanceKm - rule.IncludedKm, 2, MidpointRounding.AwayFromZero)) * rule.PerKmFee
            : 0m;

        distanceFee = decimal.Round(distanceFee, 2, MidpointRounding.AwayFromZero);

        var activeMultiplier = ResolveActiveSurgeMultiplier(rule.SurgeWindows.ToArray());
        var surgeFee = activeMultiplier > 1m
            ? decimal.Round((baseFee + distanceFee) * (activeMultiplier - 1m), 2, MidpointRounding.AwayFromZero)
            : 0m;

        ApplyClamp(rule.MinFee, rule.MaxFee, ref baseFee, ref distanceFee, ref surgeFee);

        return new DeliveryPriceQuote(
            baseFee,
            distanceFee,
            surgeFee,
            decimal.Round(baseFee + distanceFee + surgeFee, 2, MidpointRounding.AwayFromZero),
            decimal.Round(distanceKm, 2, MidpointRounding.AwayFromZero),
            pricingMode,
            rule.Name);
    }

    private static Domain.Modules.Delivery.Entities.DeliveryPricingRule? ResolveRule(
        IReadOnlyCollection<Domain.Modules.Delivery.Entities.DeliveryPricingRule> rules,
        Guid? zoneId,
        string? addressCity,
        string? vendorCity)
    {
        if (zoneId.HasValue)
        {
            var zoneRule = rules.FirstOrDefault(item => item.DeliveryZoneId == zoneId.Value);
            if (zoneRule is not null)
            {
                return zoneRule;
            }
        }

        var city = !string.IsNullOrWhiteSpace(addressCity) ? addressCity : vendorCity;
        if (string.IsNullOrWhiteSpace(city))
        {
            return null;
        }

        return rules.FirstOrDefault(item =>
                item.DeliveryZoneId != null &&
                item.DeliveryZone != null &&
                CityMatches(item.DeliveryZone.City, city))
            ?? rules.FirstOrDefault(item =>
                item.DeliveryZoneId == null &&
                CityMatches(item.City, city));
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
    {
        var normalizedLeft = NormalizeCity(left);
        var normalizedRight = NormalizeCity(right);

        return !string.IsNullOrWhiteSpace(normalizedLeft)
            && string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

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
            "الرياض" or "رياض" or "riyadh" => "riyadh",
            "جدة" or "جده" or "jeddah" => "jeddah",
            "الخبر" or "خبر" or "khobar" or "alkhobar" => "khobar",
            "الدمام" or "دمام" or "dammam" => "dammam",
            _ => normalized
        };
    }

    private static decimal ResolveActiveSurgeMultiplier(IReadOnlyCollection<Domain.Modules.Delivery.Entities.DeliveryPricingSurgeWindow> windows)
    {
        var now = DateTime.Now.TimeOfDay;

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

        if (total <= maxFee)
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
}
