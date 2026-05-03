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

        var totalOrders = await ordersQuery.CountAsync(cancellationToken);
        var completedOrders = await ordersQuery
            .CountAsync(order => order.Status == OrderStatus.Delivered, cancellationToken);
        var cancelledOrders = await ordersQuery
            .CountAsync(order => CancellationStatuses.Contains(order.Status), cancellationToken);
        var totalRevenue = await ordersQuery
            .Where(order => !RevenueExcludedStatuses.Contains(order.Status))
            .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken)
            ?? 0m;

        var groupedTrend = await ordersQuery
            .GroupBy(order => order.PlacedAtUtc.Date)
            .Select(group => new
            {
                Date = group.Key,
                OrdersCount = group.Count(),
                Revenue = group
                    .Where(order => !RevenueExcludedStatuses.Contains(order.Status))
                    .Sum(order => (decimal?)order.TotalAmount) ?? 0m
            })
            .ToDictionaryAsync(item => item.Date, item => new { item.OrdersCount, item.Revenue }, cancellationToken);

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

        var statusBreakdown = (await ordersQuery
            .GroupBy(order => order.Status == OrderStatus.Delivered
                ? "completed"
                : CancellationStatuses.Contains(order.Status)
                    ? "cancelled"
                    : order.Status == OrderStatus.DeliveryFailed || order.Status == OrderStatus.Refunded
                        ? "failed"
                        : order.Status == OrderStatus.Placed || order.Status == OrderStatus.PendingVendorAcceptance
                            ? "awaiting_action"
                            : "in_progress")
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ToListAsync(cancellationToken))
            .Select(item => new AdminVendorAnalyticsStatusBreakdownDto(
                item.Status,
                item.Count,
                totalOrders > 0 ? Math.Round((decimal)item.Count * 100m / totalOrders, 1) : 0m))
            .ToList();

        var productsQuery = _context.VendorProducts
            .AsNoTracking()
            .Where(product => product.VendorId == request.VendorId);

        var productHealth = new AdminVendorAnalyticsProductHealthDto(
            await productsQuery.CountAsync(
                product => product.Status == VendorProductStatus.Active && product.IsAvailable && product.StockQuantity > 5,
                cancellationToken),
            await productsQuery.CountAsync(
                product => product.Status == VendorProductStatus.Active && product.IsAvailable && product.StockQuantity > 0 && product.StockQuantity <= 5,
                cancellationToken),
            await productsQuery.CountAsync(
                product => product.Status == VendorProductStatus.OutOfStock || product.StockQuantity <= 0,
                cancellationToken),
            await productsQuery.CountAsync(
                product => product.Status == VendorProductStatus.Inactive || product.Status == VendorProductStatus.Suspended,
                cancellationToken));

        var topProducts = await _context.OrderItems
            .AsNoTracking()
            .Where(item =>
                item.Order.VendorId == request.VendorId &&
                item.Order.PlacedAtUtc >= fromUtc &&
                item.Order.PlacedAtUtc <= toUtc &&
                item.Order.Status != OrderStatus.PendingPayment &&
                !CancellationStatuses.Contains(item.Order.Status))
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
            .ToListAsync(cancellationToken);

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
