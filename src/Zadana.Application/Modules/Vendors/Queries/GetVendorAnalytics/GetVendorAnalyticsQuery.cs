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

        // Safely query orders
        int totalOrders = 0, completedOrders = 0, cancelledOrders = 0;
        decimal totalRevenue = 0m;
        List<AdminVendorAnalyticsTrendPointDto> salesTrend = [];
        List<AdminVendorAnalyticsStatusBreakdownDto> statusBreakdown = [];
        List<AdminVendorAnalyticsTopProductDto> topProducts = [];
        var productHealth = new AdminVendorAnalyticsProductHealthDto(0, 0, 0, 0);

        try
        {
            var ordersQuery = _context.Orders
                .AsNoTracking()
                .Where(order =>
                    order.VendorId == request.VendorId &&
                    order.Status != OrderStatus.PendingPayment &&
                    order.PlacedAtUtc >= fromUtc &&
                    order.PlacedAtUtc <= toUtc);

            totalOrders = await ordersQuery.CountAsync(cancellationToken);
            completedOrders = await ordersQuery.CountAsync(order => order.Status == OrderStatus.Delivered, cancellationToken);
            cancelledOrders = await ordersQuery.CountAsync(order => order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.VendorRejected, cancellationToken);
            totalRevenue = await ordersQuery
                .Where(order => order.Status != OrderStatus.Cancelled && order.Status != OrderStatus.VendorRejected)
                .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;

            // Sales trend
            var trendData = await ordersQuery
                .GroupBy(order => order.PlacedAtUtc.Date)
                .Select(group => new { Date = group.Key, Count = group.Count(), Revenue = group.Sum(o => (decimal?)o.TotalAmount) ?? 0m })
                .ToListAsync(cancellationToken);

            var trendDict = trendData.ToDictionary(x => x.Date, x => x);
            salesTrend = Enumerable.Range(0, rangeDays)
                .Select(offset =>
                {
                    var date = fromUtc.Date.AddDays(offset);
                    trendDict.TryGetValue(date, out var point);
                    return new AdminVendorAnalyticsTrendPointDto(date, point?.Count ?? 0, point?.Revenue ?? 0m);
                })
                .ToList();

            // Status breakdown
            var statusData = await ordersQuery
                .GroupBy(order => order.Status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);

            statusBreakdown = statusData.Select(item =>
            {
                var label = item.Status == OrderStatus.Delivered ? "completed"
                    : item.Status == OrderStatus.Cancelled || item.Status == OrderStatus.VendorRejected ? "cancelled"
                    : item.Status == OrderStatus.DeliveryFailed || item.Status == OrderStatus.Refunded ? "failed"
                    : item.Status == OrderStatus.Placed || item.Status == OrderStatus.PendingVendorAcceptance ? "awaiting_action"
                    : "in_progress";
                return new AdminVendorAnalyticsStatusBreakdownDto(label, item.Count, totalOrders > 0 ? Math.Round((decimal)item.Count * 100m / totalOrders, 1) : 0m);
            }).ToList();
        }
        catch { /* Orders query failed - return zeros */ }

        try
        {
            var productsQuery = _context.VendorProducts.AsNoTracking().Where(p => p.VendorId == request.VendorId);
            productHealth = new AdminVendorAnalyticsProductHealthDto(
                await productsQuery.CountAsync(p => p.Status == VendorProductStatus.Active && p.IsAvailable && p.StockQuantity > 5, cancellationToken),
                await productsQuery.CountAsync(p => p.Status == VendorProductStatus.Active && p.StockQuantity > 0 && p.StockQuantity <= 5, cancellationToken),
                await productsQuery.CountAsync(p => p.Status == VendorProductStatus.OutOfStock || p.StockQuantity <= 0, cancellationToken),
                await productsQuery.CountAsync(p => p.Status == VendorProductStatus.Inactive || p.Status == VendorProductStatus.Suspended, cancellationToken));
        }
        catch { /* Products query failed */ }

        try
        {
            topProducts = await _context.OrderItems
                .AsNoTracking()
                .Where(item =>
                    item.Order.VendorId == request.VendorId &&
                    item.Order.PlacedAtUtc >= fromUtc &&
                    item.Order.PlacedAtUtc <= toUtc &&
                    item.Order.Status != OrderStatus.PendingPayment &&
                    item.Order.Status != OrderStatus.Cancelled &&
                    item.Order.Status != OrderStatus.VendorRejected)
                .GroupBy(item => new { item.VendorProductId, item.ProductName })
                .Select(group => new AdminVendorAnalyticsTopProductDto(
                    group.Key.VendorProductId,
                    group.Key.ProductName,
                    group.Sum(item => item.Quantity),
                    group.Sum(item => item.LineTotal),
                    group.Select(item => item.OrderId).Distinct().Count()))
                .OrderByDescending(item => item.Revenue)
                .Take(5)
                .ToListAsync(cancellationToken);
        }
        catch { /* Top products query failed */ }

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
