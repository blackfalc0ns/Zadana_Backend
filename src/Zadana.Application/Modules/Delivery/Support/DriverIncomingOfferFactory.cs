using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Application.Modules.Delivery.Support;

public static class DriverIncomingOfferFactory
{
    public static DriverIncomingOfferDto Build(
        DeliveryAssignment assignment,
        CustomerAddress? customerAddress,
        DateTime? utcNow = null) =>
        Build(assignment, assignment.Order, customerAddress, utcNow);

    public static DriverIncomingOfferDto Build(
        DeliveryAssignment assignment,
        Order order,
        CustomerAddress? customerAddress,
        DateTime? utcNow = null)
    {
        var vendor = order.Vendor;
        var now = utcNow ?? DateTime.UtcNow;

        var pickupLatitude = order.VendorBranch?.Latitude;
        var pickupLongitude = order.VendorBranch?.Longitude;
        var deliveryLatitude = customerAddress?.Latitude;
        var deliveryLongitude = customerAddress?.Longitude;

        var distanceKm = deliveryLatitude.HasValue && deliveryLongitude.HasValue
            ? ApproximateDistanceKm(
                pickupLatitude ?? 0m,
                pickupLongitude ?? 0m,
                deliveryLatitude.Value,
                deliveryLongitude.Value)
            : 0m;

        var countdownSeconds = assignment.OfferExpiresAtUtc.HasValue
            ? Math.Max(0, (int)(assignment.OfferExpiresAtUtc.Value - now).TotalSeconds)
            : 0;

        var customerName = customerAddress?.ContactName ?? "Customer";
        var vendorNameEn = vendor.BusinessNameEn;
        var vendorNameAr = vendor.BusinessNameAr;

        return new DriverIncomingOfferDto(
            assignment.Id,
            assignment.OrderId,
            order.OrderNumber,
            vendorNameEn,
            vendorNameAr,
            vendorNameEn,
            vendor.LogoUrl,
            order.VendorBranch?.AddressLine ?? vendor.NationalAddress ?? string.Empty,
            pickupLatitude,
            pickupLongitude,
            customerName,
            BuildFullCustomerAddress(customerAddress),
            deliveryLatitude,
            deliveryLongitude,
            Math.Round(distanceKm, 2),
            BuildEta(distanceKm),
            order.DeliveryFee,
            order.PaymentMethod.ToString(),
            order.TotalAmount,
            ResolveCodAmount(assignment),
            BuildInitials(vendorNameEn),
            BuildInitials(customerName),
            order.Notes,
            countdownSeconds,
            order.Items
                .Select(item => new DriverOfferItemDto(item.ProductName, item.Quantity, order.Notes))
                .ToArray());
    }

    private static decimal ApproximateDistanceKm(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        var dLat = (double)(lat2 - lat1) * Math.PI / 180;
        var dLng = (double)(lng2 - lng1) * Math.PI / 180;
        var avgLat = (double)(lat1 + lat2) / 2 * Math.PI / 180;
        var x = dLng * Math.Cos(avgLat);
        var y = dLat;
        return (decimal)(Math.Sqrt(x * x + y * y) * 6371);
    }

    private static string BuildEta(decimal distanceKm)
    {
        var minutes = Math.Max(8, (int)Math.Round((double)distanceKm * 4));
        return $"{minutes}-{minutes + 5} min";
    }

    private static string BuildInitials(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }

    private static string BuildFullCustomerAddress(CustomerAddress? address)
    {
        if (address is null)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        var isArabic = !string.IsNullOrWhiteSpace(address.AddressLine) &&
            address.AddressLine.Any(character => character is >= '\u0600' and <= '\u06FF');

        AddAddressPart(parts, address.AddressLine);
        AddAddressPart(parts, FormatAddressDetail(address.BuildingNo, isArabic ? "مبنى" : "Building"));
        AddAddressPart(parts, FormatAddressDetail(address.FloorNo, isArabic ? "الدور" : "Floor"));
        AddAddressPart(parts, FormatAddressDetail(address.ApartmentNo, isArabic ? "شقة" : "Apartment"));
        AddAddressPart(parts, address.Area);
        AddAddressPart(parts, address.City);

        var joined = string.Join(", ", parts);
        if (!string.IsNullOrWhiteSpace(joined))
        {
            return joined;
        }

        var simple = string.Join(
            ", ",
            new[] { address.AddressLine, address.Area, address.City }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(simple))
        {
            return simple;
        }

        if (address.Latitude is decimal latitude && address.Longitude is decimal longitude)
        {
            return $"{latitude:0.######}, {longitude:0.######}";
        }

        return string.Empty;
    }

    private static string? FormatAddressDetail(string? value, string label) =>
        string.IsNullOrWhiteSpace(value) ? null : $"{label} {value.Trim()}";

    private static void AddAddressPart(ICollection<string> parts, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var candidate = value.Trim();
        var normalizedCandidate = NormalizeAddressPart(candidate);
        if (string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return;
        }

        var alreadyIncluded = parts.Any(part =>
        {
            var normalizedPart = NormalizeAddressPart(part);
            return normalizedPart.Contains(normalizedCandidate, StringComparison.OrdinalIgnoreCase) ||
                   normalizedCandidate.Contains(normalizedPart, StringComparison.OrdinalIgnoreCase);
        });

        if (!alreadyIncluded)
        {
            parts.Add(candidate);
        }
    }

    private static string NormalizeAddressPart(string value) =>
        new(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    private static decimal ResolveCodAmount(DeliveryAssignment assignment) =>
        assignment.Order.PaymentMethod == PaymentMethodType.CashOnDelivery
            ? assignment.CodAmount
            : 0m;
}
