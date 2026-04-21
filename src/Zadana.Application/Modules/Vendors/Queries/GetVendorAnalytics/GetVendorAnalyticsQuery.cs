using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Queries.GetVendorAnalytics;

public record GetVendorAnalyticsQuery(Guid VendorId, string Range = "30d") : IRequest<AdminVendorAnalyticsDto>;

public class GetVendorAnalyticsQueryHandler : IRequestHandler<GetVendorAnalyticsQuery, AdminVendorAnalyticsDto>
{
    private static readonly OrderStatus[] CancellationStatuses = [OrderStatus.Cancelled, OrderStatus.VendorRejected];
    private static readonly OrderStatus[] RevenueExcludedStatuses = [OrderStatus.PendingPayment, OrderStatus.Cancelled, OrderStatus.VendorRejected];

    private readonly IApplicationDbContext _context;

    public GetVendorAnalyticsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminVendorAnalyticsDto> Handle(GetVendorAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var exists = await _context.Vendors
            .AsNoTracking()
            .AnyAsync(vendor => vendor.Id == request.VendorId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Vendor", request.VendorId);
        }

        var (rangeCode, rangeDays) = ResolveRange(request.Range);
        var generatedAtUtc = DateTime.UtcNow;
        var toUtc = generatedAtUtc;
        var fromUtc = generatedAtUtc.Date.AddDays(-(rangeDays - 1));

        var ordersQuery = _context.Orders
            .AsNoTracking()
            .Where(order =>
                order.VendorId == request.VendorId &&
                order.Status != OrderStatus.PendingPayment &&
                order.PlacedAtUtc >= fromUtc &&
                order.PlacedAtUtc <= toUtc);

        var orderRows = await ordersQuery
            .Select(order => new AnalyticsOrderRow(order.PlacedAtUtc, order.Status, order.TotalAmount))
            .ToListAsync(cancellationToken);

        var totalOrders = orderRows.Count;
        var completedOrders = orderRows.Count(order => order.Status == OrderStatus.Delivered);
        var cancelledOrders = orderRows.Count(order => CancellationStatuses.Contains(order.Status));
        var totalRevenue = orderRows
            .Where(order => !RevenueExcludedStatuses.Contains(order.Status))
            .Sum(order => order.TotalAmount);

        var groupedTrend = orderRows
            .GroupBy(order => order.PlacedAtUtc.Date)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    OrdersCount = group.Count(),
                    Revenue = group
                        .Where(order => !RevenueExcludedStatuses.Contains(order.Status))
                        .Sum(order => order.TotalAmount)
                });

        var salesTrend = Enumerable.Range(0, rangeDays)
            .Select(offset =>
            {
                var date = fromUtc.Date.AddDays(offset);
                groupedTrend.TryGetValue(date, out var point);

                return new AdminVendorAnalyticsTrendPointDto(
                    date,
                    point?.OrdersCount ?? 0,
                    point?.Revenue ?? 0m);
            })
            .ToList();

        var statusBreakdown = orderRows
            .GroupBy(order => MapOrderStatusBucket(order.Status))
            .Select(group =>
            {
                var count = group.Count();
                return new AdminVendorAnalyticsStatusBreakdownDto(
                    group.Key,
                    count,
                    totalOrders > 0 ? Math.Round((decimal)count * 100m / totalOrders, 1) : 0m);
            })
            .OrderByDescending(item => item.Count)
            .ToList();

        var productRows = await _context.VendorProducts
            .AsNoTracking()
            .Where(product => product.VendorId == request.VendorId)
            .Select(product => new AnalyticsProductRow(product.Status, product.IsAvailable, product.StockQuantity))
            .ToListAsync(cancellationToken);

        var productHealth = new AdminVendorAnalyticsProductHealthDto(
            productRows.Count(product => product.Status == VendorProductStatus.Active && product.IsAvailable && product.StockQuantity > 5),
            productRows.Count(product => product.Status == VendorProductStatus.Active && product.IsAvailable && product.StockQuantity > 0 && product.StockQuantity <= 5),
            productRows.Count(product => product.Status == VendorProductStatus.OutOfStock || product.StockQuantity <= 0),
            productRows.Count(product => product.Status == VendorProductStatus.Inactive || product.Status == VendorProductStatus.Suspended));

        var topProductRows = await _context.OrderItems
            .AsNoTracking()
            .Where(item =>
                item.Order.VendorId == request.VendorId &&
                item.Order.PlacedAtUtc >= fromUtc &&
                item.Order.PlacedAtUtc <= toUtc &&
                item.Order.Status != OrderStatus.PendingPayment &&
                !CancellationStatuses.Contains(item.Order.Status))
            .Select(item => new AnalyticsTopProductRow(
                item.VendorProductId,
                item.ProductName,
                item.Quantity,
                item.LineTotal,
                item.OrderId))
            .ToListAsync(cancellationToken);

        var topProducts = topProductRows
            .GroupBy(item => new { item.VendorProductId, item.ProductName })
            .Select(group => new AdminVendorAnalyticsTopProductDto(
                group.Key.VendorProductId,
                group.Key.ProductName,
                group.Sum(item => item.Quantity),
                group.Sum(item => item.LineTotal),
                group.Select(item => item.OrderId).Distinct().Count()))
            .OrderByDescending(item => item.Revenue)
            .ThenByDescending(item => item.UnitsSold)
            .Take(5)
            .ToList();

        var summary = new AdminVendorAnalyticsSummaryDto(
            totalRevenue,
            totalOrders,
            totalOrders > 0 ? Math.Round(totalRevenue / totalOrders, 2) : 0m,
            totalOrders > 0 ? Math.Round((decimal)completedOrders * 100m / totalOrders, 1) : 0m,
            totalOrders > 0 ? Math.Round((decimal)cancelledOrders * 100m / totalOrders, 1) : 0m,
            productHealth.Available,
            productHealth.LowStock + productHealth.OutOfStock);

        return new AdminVendorAnalyticsDto(
            summary,
            salesTrend,
            statusBreakdown,
            productHealth,
            topProducts,
            new AdminVendorAnalyticsMetaDto(rangeCode, fromUtc, toUtc, generatedAtUtc));
    }

    private static (string RangeCode, int RangeDays) ResolveRange(string? range) => range?.Trim().ToLowerInvariant() switch
    {
        "7d" => ("7d", 7),
        "90d" => ("90d", 90),
        _ => ("30d", 30)
    };

    private static string MapOrderStatusBucket(OrderStatus status) => status switch
    {
        OrderStatus.Delivered => "completed",
        OrderStatus.Cancelled or OrderStatus.VendorRejected => "cancelled",
        OrderStatus.DeliveryFailed or OrderStatus.Refunded => "failed",
        OrderStatus.Placed or OrderStatus.PendingVendorAcceptance => "awaiting_action",
        _ => "in_progress"
    };

    private sealed record AnalyticsOrderRow(DateTime PlacedAtUtc, OrderStatus Status, decimal TotalAmount);
    private sealed record AnalyticsProductRow(VendorProductStatus Status, bool IsAvailable, int StockQuantity);
    private sealed record AnalyticsTopProductRow(Guid VendorProductId, string ProductName, int Quantity, decimal LineTotal, Guid OrderId);
}

public record AdminVendorAnalyticsDto(
    AdminVendorAnalyticsSummaryDto Summary,
    IReadOnlyList<AdminVendorAnalyticsTrendPointDto> SalesTrend,
    IReadOnlyList<AdminVendorAnalyticsStatusBreakdownDto> OrderStatusBreakdown,
    AdminVendorAnalyticsProductHealthDto ProductHealth,
    IReadOnlyList<AdminVendorAnalyticsTopProductDto> TopProducts,
    AdminVendorAnalyticsMetaDto Meta);

public record AdminVendorAnalyticsSummaryDto(
    decimal TotalRevenue,
    int TotalOrders,
    decimal AverageOrderValue,
    decimal CompletionRate,
    decimal CancellationRate,
    int AvailableProducts,
    int LowStockProducts);

public record AdminVendorAnalyticsTrendPointDto(
    DateTime Date,
    int OrdersCount,
    decimal Revenue);

public record AdminVendorAnalyticsStatusBreakdownDto(
    string Status,
    int Count,
    decimal Percentage);

public record AdminVendorAnalyticsProductHealthDto(
    int Available,
    int LowStock,
    int OutOfStock,
    int Inactive);

public record AdminVendorAnalyticsTopProductDto(
    Guid VendorProductId,
    string ProductName,
    int UnitsSold,
    decimal Revenue,
    int OrdersCount);

public record AdminVendorAnalyticsMetaDto(
    string Range,
    DateTime FromUtc,
    DateTime ToUtc,
    DateTime GeneratedAtUtc);
