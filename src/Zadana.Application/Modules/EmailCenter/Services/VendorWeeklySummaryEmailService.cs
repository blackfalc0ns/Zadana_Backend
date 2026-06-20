using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.EmailCenter.DTOs;
using Zadana.Application.Modules.EmailCenter.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Application.Modules.EmailCenter.Services;

public sealed class VendorWeeklySummaryEmailService : IVendorWeeklySummaryEmailService
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailCenterService _emailCenterService;
    private readonly ILogger<VendorWeeklySummaryEmailService> _logger;

    public VendorWeeklySummaryEmailService(
        IApplicationDbContext context,
        IEmailCenterService emailCenterService,
        ILogger<VendorWeeklySummaryEmailService> logger)
    {
        _context = context;
        _emailCenterService = emailCenterService;
        _logger = logger;
    }

    public async Task<int> DispatchWeeklySummariesAsync(
        DateTime weekStartUtc,
        DateTime weekEndUtc,
        CancellationToken cancellationToken = default)
    {
        if (weekEndUtc <= weekStartUtc)
        {
            throw new ArgumentException("Week end must be after week start.", nameof(weekEndUtc));
        }

        var summaries = await _context.Orders
            .AsNoTracking()
            .Where(order => order.PlacedAtUtc >= weekStartUtc && order.PlacedAtUtc < weekEndUtc)
            .GroupBy(order => order.VendorId)
            .Select(group => new VendorWeeklyOrderSummary(
                group.Key,
                group.Count(),
                group.Count(order => order.Status == OrderStatus.Delivered),
                group.Count(order =>
                    order.Status == OrderStatus.Cancelled ||
                    order.Status == OrderStatus.VendorRejected ||
                    order.Status == OrderStatus.DeliveryFailed ||
                    order.Status == OrderStatus.Refunded),
                group.Sum(order => order.Status == OrderStatus.Delivered ? order.TotalAmount : 0m)))
            .ToListAsync(cancellationToken);

        if (summaries.Count == 0)
        {
            return 0;
        }

        var summaryByVendorId = summaries.ToDictionary(item => item.VendorId);
        var vendorIds = summaryByVendorId.Keys.ToList();

        var vendors = await _context.Vendors
            .AsNoTracking()
            .Where(vendor =>
                vendorIds.Contains(vendor.Id) &&
                vendor.Status == VendorStatus.Active &&
                vendor.EmailNotificationsEnabled)
            .Select(vendor => new VendorWeeklyRecipient(
                vendor.Id,
                string.IsNullOrWhiteSpace(vendor.BusinessNameEn) ? vendor.BusinessNameAr : vendor.BusinessNameEn,
                vendor.OwnerEmail,
                vendor.ContactEmail))
            .ToListAsync(cancellationToken);

        if (vendors.Count == 0)
        {
            return 0;
        }

        var topProducts = await _context.OrderItems
            .AsNoTracking()
            .Where(item =>
                vendorIds.Contains(item.Order.VendorId) &&
                item.Order.PlacedAtUtc >= weekStartUtc &&
                item.Order.PlacedAtUtc < weekEndUtc)
            .GroupBy(item => new { item.Order.VendorId, item.ProductName })
            .Select(group => new VendorTopProduct(
                group.Key.VendorId,
                group.Key.ProductName,
                group.Sum(item => item.Quantity)))
            .ToListAsync(cancellationToken);

        var topProductsByVendor = topProducts
            .GroupBy(item => item.VendorId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Quantity)
                    .ThenBy(item => item.ProductName)
                    .Take(3)
                    .ToList());

        var sentCount = 0;
        foreach (var vendor in vendors)
        {
            var summary = summaryByVendorId[vendor.Id];
            if (summary.TotalOrders == 0)
            {
                continue;
            }

            try
            {
                var recipient = ResolveFirstEmail(vendor.OwnerEmail, vendor.ContactEmail);
                var weekLabel = $"{weekStartUtc:yyyy-MM-dd} to {weekEndUtc.AddDays(-1):yyyy-MM-dd}";
                var result = await _emailCenterService.DispatchSystemEventEmailAsync(
                    new EmailSystemEventDispatchRequest(
                        EventKey: EmailEventKeys.VendorWeeklySummary,
                        AudienceType: "vendor_network",
                        To: string.IsNullOrWhiteSpace(recipient) ? [] : [recipient],
                        Variables: new Dictionary<string, string>
                        {
                            ["vendor_name"] = vendor.Name,
                            ["week_label"] = weekLabel,
                            ["summary_body"] = BuildSummaryBody(
                                summary,
                                topProductsByVendor.GetValueOrDefault(vendor.Id) ?? [])
                        },
                        TargetUrl: "/dashboard",
                        VendorId: vendor.Id,
                        DuplicateWindowStartUtc: weekEndUtc,
                        DuplicateWindowEndUtc: weekEndUtc.AddDays(7)),
                    cancellationToken);

                if (result.Sent)
                {
                    sentCount++;
                }
                else if (!result.Skipped)
                {
                    _logger.LogWarning(
                        "Vendor weekly summary email failed for vendor {VendorId}. Reason: {Reason}",
                        vendor.Id,
                        result.Reason);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Vendor weekly summary email dispatch threw for vendor {VendorId}.",
                    vendor.Id);
            }
        }

        return sentCount;
    }

    private static string BuildSummaryBody(
        VendorWeeklyOrderSummary summary,
        IReadOnlyList<VendorTopProduct> topProducts)
    {
        var topProductsText = topProducts.Count == 0
            ? "No product ranking this week."
            : string.Join("<br>", topProducts.Select((item, index) => $"{index + 1}. {item.ProductName} - {item.Quantity} sold"));

        return string.Join("<br>", new[]
        {
            $"Total sales: {summary.CompletedSales:0.##} SAR",
            $"Orders: {summary.TotalOrders}",
            $"Completed: {summary.CompletedOrders}",
            $"Cancelled/failed/refunded: {summary.ExceptionOrders}",
            "Top products:",
            topProductsText
        });
    }

    private static string? ResolveFirstEmail(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private sealed record VendorWeeklyOrderSummary(
        Guid VendorId,
        int TotalOrders,
        int CompletedOrders,
        int ExceptionOrders,
        decimal CompletedSales);

    private sealed record VendorWeeklyRecipient(
        Guid Id,
        string Name,
        string? OwnerEmail,
        string ContactEmail);

    private sealed record VendorTopProduct(Guid VendorId, string ProductName, int Quantity);
}
