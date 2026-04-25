using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/vendor")]
[Tags("Vendor App API")]
[Authorize(Policy = "VendorOnly")]
public class VendorWorkspaceController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentVendorService _currentVendorService;

    public VendorWorkspaceController(IApplicationDbContext dbContext, ICurrentVendorService currentVendorService)
    {
        _dbContext = dbContext;
        _currentVendorService = currentVendorService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<VendorDashboardSnapshotResponse>> GetDashboard(CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var since = DateTime.UtcNow.AddDays(-30);

        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.VendorId == vendorId)
            .OrderByDescending(order => order.PlacedAtUtc)
            .Select(order => new
            {
                order.Id,
                order.OrderNumber,
                order.Status,
                order.PaymentStatus,
                order.TotalAmount,
                order.PlacedAtUtc
            })
            .Take(50)
            .ToListAsync(cancellationToken);

        var totalSales = orders
            .Where(order => order.PaymentStatus == PaymentStatus.Paid || order.PaymentStatus == PaymentStatus.Settled || order.Status == OrderStatus.Delivered)
            .Sum(order => order.TotalAmount);

        var pendingOrders = orders.Count(order =>
            order.Status is OrderStatus.Placed or OrderStatus.PendingVendorAcceptance or OrderStatus.Accepted or OrderStatus.Preparing);

        var lowStockCount = await _dbContext.VendorProducts
            .AsNoTracking()
            .CountAsync(product => product.VendorId == vendorId && product.StockQuantity > 0 && product.StockQuantity <= 5, cancellationToken);

        var activeProducts = await _dbContext.VendorProducts
            .AsNoTracking()
            .CountAsync(product => product.VendorId == vendorId && product.IsAvailable, cancellationToken);

        var recentReviews = await _dbContext.Reviews
            .AsNoTracking()
            .Where(review => review.VendorId == vendorId)
            .OrderByDescending(review => review.CreatedAtUtc)
            .Take(3)
            .Select(review => new { review.Rating, review.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var recentTimeline = orders.Take(3).Select(order => new VendorDashboardTimelineItemResponse(
                order.PlacedAtUtc.ToString("HH:mm"),
                $"طلب #{order.OrderNumber} بحالة {order.Status}"))
            .Concat(recentReviews.Select(review => new VendorDashboardTimelineItemResponse(
                review.CreatedAtUtc.ToString("HH:mm"),
                $"تقييم جديد {review.Rating}/5")))
            .Take(5)
            .ToList();

        if (recentTimeline.Count == 0)
        {
            recentTimeline.Add(new VendorDashboardTimelineItemResponse(DateTime.UtcNow.ToString("HH:mm"), "لا توجد أحداث تشغيلية بعد"));
        }

        var checklist = new List<VendorDashboardChecklistItemResponse>();
        if (pendingOrders > 0)
        {
            checklist.Add(new VendorDashboardChecklistItemResponse(
                "DASHBOARD.CHECKLIST.CONFIRM_ORDERS_TITLE",
                "DASHBOARD.CHECKLIST.CONFIRM_ORDERS_BODY"));
        }

        if (lowStockCount > 0)
        {
            checklist.Add(new VendorDashboardChecklistItemResponse(
                "DASHBOARD.CHECKLIST.LOW_STOCK_TITLE",
                "DASHBOARD.CHECKLIST.LOW_STOCK_BODY"));
        }

        if (activeProducts == 0)
        {
            checklist.Add(new VendorDashboardChecklistItemResponse(
                "DASHBOARD.ADD_PRODUCTS",
                "DASHBOARD.ADD_PRODUCTS_DESC"));
        }

        if (checklist.Count == 0)
        {
            checklist.Add(new VendorDashboardChecklistItemResponse(
                "DASHBOARD.CHECKLIST.REFRESH_OFFERS_TITLE",
                "كل المؤشرات الأساسية مستقرة خلال آخر 30 يوم."));
        }

        return Ok(new VendorDashboardSnapshotResponse(
            [
                new VendorDashboardMetricResponse(totalSales.ToString("N0"), "DASHBOARD.TOTAL_SALES", "DASHBOARD.TOTAL_SALES_NOTE", true),
                new VendorDashboardMetricResponse(activeProducts.ToString("N0"), "DASHBOARD.ACTIVE_OFFERS", "منتجات متاحة للبيع حاليًا", false),
                new VendorDashboardMetricResponse(pendingOrders.ToString("N0"), "DASHBOARD.PENDING_ORDERS", "DASHBOARD.PENDING_ORDERS_NOTE", false)
            ],
            checklist,
            [
                new VendorDashboardQuickActionResponse("DASHBOARD.ADD_PRODUCTS", "DASHBOARD.ADD_PRODUCTS_DESC", "warm"),
                new VendorDashboardQuickActionResponse("DASHBOARD.TRACK_SHIPMENTS", "DASHBOARD.TRACK_SHIPMENTS_DESC", "soft"),
                new VendorDashboardQuickActionResponse("DASHBOARD.ADJUST_HOURS", "DASHBOARD.ADJUST_HOURS_DESC", "dark")
            ],
            recentTimeline));
    }

    [HttpGet("finance")]
    public async Task<ActionResult<VendorFinanceSnapshotResponse>> GetFinance(
        [FromQuery] string period = "month",
        CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var from = ResolvePeriodStart(period);

        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.VendorId == vendorId && order.PlacedAtUtc >= from)
            .OrderBy(order => order.PlacedAtUtc)
            .Select(order => new
            {
                order.Id,
                order.OrderNumber,
                order.Status,
                order.PaymentStatus,
                order.TotalAmount,
                order.CommissionAmount,
                order.PlacedAtUtc
            })
            .ToListAsync(cancellationToken);

        var settlements = await _dbContext.Settlements
            .AsNoTracking()
            .Where(settlement => settlement.VendorId == vendorId)
            .OrderByDescending(settlement => settlement.CreatedAtUtc)
            .Take(6)
            .Select(settlement => new
            {
                settlement.Id,
                settlement.Status,
                settlement.NetAmount,
                settlement.CreatedAtUtc,
                settlement.ProcessedAtUtc,
                OrdersCount = settlement.Items.Count
            })
            .ToListAsync(cancellationToken);

        var payouts = await _dbContext.Payouts
            .AsNoTracking()
            .Where(payout => payout.Settlement.VendorId == vendorId)
            .OrderByDescending(payout => payout.CreatedAtUtc)
            .Take(8)
            .Select(payout => new
            {
                payout.Id,
                payout.Status,
                payout.Amount,
                payout.CreatedAtUtc,
                payout.ProcessedAtUtc,
                payout.TransferReference
            })
            .ToListAsync(cancellationToken);

        var primaryBank = await _dbContext.VendorBankAccounts
            .AsNoTracking()
            .Where(account => account.VendorId == vendorId && account.IsPrimary)
            .Select(account => new { account.BankName })
            .FirstOrDefaultAsync(cancellationToken);

        var paidOrders = orders.Where(order =>
            order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Settled || order.Status == OrderStatus.Delivered).ToList();
        var netSales = paidOrders.Sum(order => order.TotalAmount);
        var fees = paidOrders.Sum(order => order.CommissionAmount);
        var payoutsPaid = payouts.Where(payout => payout.Status == PayoutStatus.Paid).Sum(payout => payout.Amount);
        var pendingSettlement = settlements
            .Where(settlement => settlement.Status is SettlementStatus.Pending or SettlementStatus.Processing)
            .Sum(settlement => settlement.NetAmount);
        var availableBalance = Math.Max(0, netSales - fees - payoutsPaid);

        var trend = BuildFinanceTrend(paidOrders);
        var ledger = paidOrders
            .OrderByDescending(order => order.PlacedAtUtc)
            .Take(8)
            .Select(order => new VendorLedgerEntryResponse(
                order.Id.ToString(),
                order.PlacedAtUtc.ToString("yyyy-MM-dd"),
                "مبيعات طلب",
                "Order sales",
                "sale",
                order.TotalAmount,
                "in",
                order.OrderNumber))
            .Concat(payouts.Take(5).Select(payout => new VendorLedgerEntryResponse(
                payout.Id.ToString(),
                (payout.ProcessedAtUtc ?? payout.CreatedAtUtc).ToString("yyyy-MM-dd"),
                "تحويل بنكي",
                "Bank payout",
                "payout",
                payout.Amount,
                "out",
                payout.TransferReference ?? $"PAY-{payout.Id.ToString()[..8]}")))
            .OrderByDescending(entry => entry.Date)
            .Take(10)
            .ToList();

        var nextPayoutDate = settlements
            .Where(settlement => settlement.Status is SettlementStatus.Pending or SettlementStatus.Processing)
            .OrderBy(settlement => settlement.CreatedAtUtc)
            .Select(settlement => settlement.ProcessedAtUtc ?? settlement.CreatedAtUtc.AddDays(7))
            .FirstOrDefault();

        return Ok(new VendorFinanceSnapshotResponse(
            availableBalance,
            pendingSettlement,
            nextPayoutDate == default ? DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd") : nextPayoutDate.ToString("yyyy-MM-dd"),
            primaryBank is null ? "Bank transfer" : $"Bank Transfer - {primaryBank.BankName}",
            Math.Max(0, pendingSettlement * 0.05m),
            [
                new VendorFinanceKpiResponse("net-sales", "VENDOR_FINANCE.KPIS.NET_SALES", netSales, 0, "up", "primary"),
                new VendorFinanceKpiResponse("vendor-payouts", "VENDOR_FINANCE.KPIS.PAYOUTS", payoutsPaid, 0, "up", "success"),
                new VendorFinanceKpiResponse("fees", "VENDOR_FINANCE.KPIS.FEES", fees, 0, "down", "warning"),
                new VendorFinanceKpiResponse("refunds", "VENDOR_FINANCE.KPIS.REFUNDS", orders.Where(order => order.PaymentStatus == PaymentStatus.Refunded).Sum(order => order.TotalAmount), 0, "down", "danger")
            ],
            trend,
            settlements.Select(settlement => new VendorSettlementResponse(
                settlement.Id.ToString(),
                $"SET-{settlement.CreatedAtUtc:yyMMdd}",
                settlement.CreatedAtUtc.ToString("yyyy-MM-dd"),
                MapSettlementStatus(settlement.Status),
                settlement.NetAmount,
                settlement.OrdersCount)).ToList(),
            ledger,
            BuildFinanceAlerts(pendingSettlement, availableBalance)));
    }

    [HttpGet("finance/ledger")]
    public async Task<ActionResult<VendorFinanceLedgerPageResponse>> GetFinanceLedger(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.VendorId == vendorId)
            .OrderByDescending(order => order.PlacedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var orders = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new
            {
                order.Id,
                order.PlacedAtUtc,
                order.TotalAmount,
                order.OrderNumber
            })
            .ToListAsync(cancellationToken);

        var items = orders
            .Select(order => new VendorLedgerEntryResponse(
                order.Id.ToString(),
                order.PlacedAtUtc.ToString("yyyy-MM-dd"),
                "مبيعات طلب",
                "Order sales",
                "sale",
                order.TotalAmount,
                "in",
                order.OrderNumber))
            .ToList();

        return Ok(new VendorFinanceLedgerPageResponse(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }

    [HttpGet("reviews")]
    public async Task<ActionResult<List<VendorReviewResponse>>> GetReviews(CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);

        var reviews = await _dbContext.Reviews
            .AsNoTracking()
            .Where(review => review.VendorId == vendorId)
            .OrderByDescending(review => review.CreatedAtUtc)
            .Select(review => new
            {
                review.Id,
                review.OrderId,
                review.Rating,
                review.Comment,
                review.CreatedAtUtc,
                review.VendorReply,
                review.VendorRepliedAtUtc,
                review.VendorReplyUpdatedAtUtc,
                CustomerName = review.User.FullName,
                OrderNumber = review.Order.OrderNumber
            })
            .ToListAsync(cancellationToken);

        return Ok(reviews.Select(review => new VendorReviewResponse(
            review.Id.ToString(),
            "order",
            review.CustomerName,
            MaskName(review.CustomerName),
            review.Rating,
            $"تقييم الطلب #{review.OrderNumber}",
            review.Comment ?? string.Empty,
            review.CreatedAtUtc,
            "published",
            string.IsNullOrWhiteSpace(review.VendorReply) ? "none" : "replied",
            review.Rating <= 2 && string.IsNullOrWhiteSpace(review.VendorReply) ? "needs_attention" : "normal",
            true,
            null,
            null,
            review.OrderId.ToString(),
            review.OrderNumber,
            null,
            null,
            null,
            string.IsNullOrWhiteSpace(review.VendorReply)
                ? null
                : new VendorReviewReplyResponse(review.VendorReply, review.VendorRepliedAtUtc ?? review.CreatedAtUtc, review.VendorReplyUpdatedAtUtc),
            [])).ToList());
    }

    [HttpPost("reviews/{reviewId:guid}/reply")]
    public async Task<ActionResult<VendorReviewReplyResponse>> ReplyToReview(
        Guid reviewId,
        [FromBody] VendorReviewReplyRequest request,
        CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var review = await _dbContext.Reviews
            .FirstOrDefaultAsync(item => item.Id == reviewId && item.VendorId == vendorId, cancellationToken);

        if (review is null)
        {
            throw new NotFoundException("Review", reviewId);
        }

        review.SetVendorReply(request.Message);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new VendorReviewReplyResponse(
            review.VendorReply!,
            review.VendorRepliedAtUtc ?? DateTime.UtcNow,
            review.VendorReplyUpdatedAtUtc));
    }

    private static DateTime ResolvePeriodStart(string period)
    {
        var now = DateTime.UtcNow;
        return period.ToLowerInvariant() switch
        {
            "today" => now.Date,
            "week" => now.Date.AddDays(-7),
            "quarter" => now.Date.AddMonths(-3),
            _ => now.Date.AddMonths(-1)
        };
    }

    private static List<VendorFinanceTrendPointResponse> BuildFinanceTrend(IReadOnlyCollection<dynamic> paidOrders)
    {
        var months = Enumerable.Range(0, 6)
            .Select(offset => DateTime.UtcNow.Date.AddMonths(-5 + offset))
            .ToList();

        return months.Select(month =>
        {
            var sales = paidOrders
                .Where(order => order.PlacedAtUtc.Year == month.Year && order.PlacedAtUtc.Month == month.Month)
                .Sum(order => (decimal)order.TotalAmount);

            return new VendorFinanceTrendPointResponse(month.ToString("MMM"), sales, Math.Max(0, sales * 0.85m));
        }).ToList();
    }

    private static List<VendorFinanceAlertResponse> BuildFinanceAlerts(decimal pendingSettlement, decimal availableBalance)
    {
        var alerts = new List<VendorFinanceAlertResponse>();
        if (pendingSettlement > 0)
        {
            alerts.Add(new VendorFinanceAlertResponse(
                "pending-settlement",
                "info",
                "VENDOR_FINANCE.ALERTS.PAYOUT_TITLE",
                "VENDOR_FINANCE.ALERTS.PAYOUT_BODY",
                "VENDOR_FINANCE.ACTIONS.DOWNLOAD_STATEMENT"));
        }

        if (availableBalance <= 0)
        {
            alerts.Add(new VendorFinanceAlertResponse(
                "balance-hold",
                "warning",
                "VENDOR_FINANCE.ALERTS.HOLD_TITLE",
                "VENDOR_FINANCE.ALERTS.HOLD_BODY",
                "VENDOR_FINANCE.ACTIONS.REVIEW_ORDERS"));
        }

        return alerts;
    }

    private static string MapSettlementStatus(SettlementStatus status) => status switch
    {
        SettlementStatus.Settled => "paid",
        SettlementStatus.Processing => "processing",
        _ => "scheduled"
    };

    private static string MaskName(string name)
    {
        return string.Join(' ', name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Length <= 2 ? $"{part[0]}*" : $"{part[..Math.Min(2, part.Length)]}{new string('*', Math.Max(1, part.Length - 2))}"));
    }
}

public record VendorDashboardSnapshotResponse(
    List<VendorDashboardMetricResponse> Metrics,
    List<VendorDashboardChecklistItemResponse> Checklist,
    List<VendorDashboardQuickActionResponse> QuickActions,
    List<VendorDashboardTimelineItemResponse> Timeline);

public record VendorDashboardMetricResponse(string Value, string LabelKey, string NoteKey, bool IsCurrency);
public record VendorDashboardChecklistItemResponse(string TitleKey, string BodyKey);
public record VendorDashboardQuickActionResponse(string TitleKey, string BodyKey, string Accent);
public record VendorDashboardTimelineItemResponse(string Time, string TitleKey);

public record VendorFinanceSnapshotResponse(
    decimal AvailableBalance,
    decimal PendingSettlement,
    string NextPayoutDate,
    string PayoutMethod,
    decimal HoldAmount,
    List<VendorFinanceKpiResponse> Kpis,
    List<VendorFinanceTrendPointResponse> Trend,
    List<VendorSettlementResponse> Settlements,
    List<VendorLedgerEntryResponse> Ledger,
    List<VendorFinanceAlertResponse> Alerts);

public record VendorFinanceKpiResponse(string Id, string LabelKey, decimal Value, decimal Delta, string Trend, string Tone);
public record VendorFinanceTrendPointResponse(string Label, decimal Sales, decimal Payout);
public record VendorSettlementResponse(string Id, string Code, string Date, string Status, decimal Amount, int OrdersCount);
public record VendorLedgerEntryResponse(string Id, string Date, string TitleAr, string TitleEn, string Type, decimal Amount, string Direction, string Reference);
public record VendorFinanceAlertResponse(string Id, string Severity, string TitleKey, string BodyKey, string ActionLabelKey);
public record VendorFinanceLedgerPageResponse(List<VendorLedgerEntryResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public record VendorReviewResponse(
    string Id,
    string Type,
    string CustomerName,
    string CustomerMaskedName,
    int Rating,
    string Title,
    string Comment,
    DateTime CreatedAt,
    string Visibility,
    string ReplyStatus,
    string AttentionState,
    bool IsVerifiedPurchase,
    string? ProductId,
    string? ProductName,
    string? OrderId,
    string? OrderDisplayId,
    int? DeliveryRating,
    int? PackagingRating,
    int? AccuracyRating,
    VendorReviewReplyResponse? VendorReply,
    List<string> Media);

public record VendorReviewReplyRequest(string Message);
public record VendorReviewReplyResponse(string Message, DateTime CreatedAt, DateTime? UpdatedAt);
