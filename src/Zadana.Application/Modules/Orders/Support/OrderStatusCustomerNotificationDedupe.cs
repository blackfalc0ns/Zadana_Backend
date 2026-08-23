using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Modules.Orders.Support;

/// <summary>
/// Stable keys for customer order-status inbox + push so retries and parallel publish paths
/// collapse to a single visible notification per status transition.
/// </summary>
public static class OrderStatusCustomerNotificationDedupe
{
    public static string BuildDedupeKey(Guid orderId, OrderStatus newStatus) =>
        newStatus is OrderStatus.Cancelled or OrderStatus.VendorRejected
            or OrderStatus.DeliveryFailed or OrderStatus.Refunded
            ? $"order-cancelled:{orderId:N}"
            : $"order-status:{orderId:N}:{newStatus}";

    public static Guid CreateStableNotificationId(string dedupeKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(dedupeKey));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    public static string? TryExtractDedupeKey(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(dataJson);
            var root = document.RootElement;
            if (root.TryGetProperty("dedupeKey", out var dedupeKey) &&
                dedupeKey.ValueKind == JsonValueKind.String)
            {
                return dedupeKey.GetString();
            }

            if (root.TryGetProperty("eventId", out var eventId) &&
                eventId.ValueKind == JsonValueKind.String)
            {
                return eventId.GetString();
            }
        }
        catch (JsonException)
        {
            // Ignore malformed notification payloads.
        }

        return null;
    }
}
