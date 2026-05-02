using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
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

    [HttpGet("dashboard/overview")]
    public async Task<ActionResult<VendorDashboardOverviewResponse>> GetDashboardOverview(
        [FromQuery] string period = "7d",
        CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var (normalizedPeriod, from, previousFrom) = ResolveDashboardPeriod(period, now);

        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.VendorId == vendorId && order.PlacedAtUtc >= from)
            .Select(order => new
            {
                order.Id,
                order.OrderNumber,
                order.Status,
                order.PaymentStatus,
                order.TotalAmount,
                order.CommissionAmount,
                order.PlacedAtUtc,
                order.DeliveredAtUtc,
                AcceptedAtUtc = order.StatusHistory.Where(h => h.NewStatus == OrderStatus.Preparing).Max(h => (DateTime?)h.CreatedAtUtc),
                ReadyAtUtc = order.StatusHistory.Where(h => h.NewStatus == OrderStatus.ReadyForPickup).Max(h => (DateTime?)h.CreatedAtUtc)
            })
            .ToListAsync(cancellationToken);

        var previousOrders = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.VendorId == vendorId && order.PlacedAtUtc >= previousFrom && order.PlacedAtUtc < from)
            .Select(order => new
            {
                order.Status,
                order.PaymentStatus,
                order.TotalAmount
            })
            .ToListAsync(cancellationToken);

        var activeOrders = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.VendorId == vendorId && order.Status != OrderStatus.Delivered && order.Status != OrderStatus.Cancelled && order.Status != OrderStatus.VendorRejected)
            .Select(order => new
            {
                order.Id,
                order.OrderNumber,
                order.Status,
                order.PlacedAtUtc
            })
            .ToListAsync(cancellationToken);

        var vendorProducts = await _dbContext.VendorProducts
            .AsNoTracking()
            .Where(product => product.VendorId == vendorId)
            .Select(product => new
            {
                product.Id,
                product.StockQuantity,
                product.IsAvailable,
                product.SellingPrice,
                product.CompareAtPrice,
                product.CreatedAtUtc,
                NameAr = product.CustomNameAr ?? product.MasterProduct.NameAr,
                NameEn = product.CustomNameEn ?? product.MasterProduct.NameEn,
                CategoryAr = product.MasterProduct.Category.NameAr,
                CategoryEn = product.MasterProduct.Category.NameEn
            })
            .ToListAsync(cancellationToken);

        var orderItems = await _dbContext.OrderItems
            .AsNoTracking()
            .Where(item => item.Order.VendorId == vendorId && item.Order.PlacedAtUtc >= from)
            .Select(item => new
            {
                item.VendorProductId,
                item.Quantity,
                item.LineTotal,
                item.ProductName,
                CategoryAr = item.MasterProduct.Category.NameAr,
                CategoryEn = item.MasterProduct.Category.NameEn
            })
            .ToListAsync(cancellationToken);

        var reviews = await _dbContext.Reviews
            .AsNoTracking()
            .Where(review => review.VendorId == vendorId)
            .Select(review => new
            {
                review.Id,
                review.Rating,
                review.Comment,
                review.CreatedAtUtc,
                review.VendorReply,
                CustomerName = review.User.FullName
            })
            .ToListAsync(cancellationToken);

        var disputes = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .Where(item => item.Order.VendorId == vendorId)
            .Select(item => new
            {
                item.Id,
                item.Type,
                item.Status,
                item.Priority,
                item.Message,
                item.VendorResponse,
                item.CreatedAtUtc,
                item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var settlements = await _dbContext.Settlements
            .AsNoTracking()
            .Where(settlement => settlement.VendorId == vendorId)
            .OrderByDescending(settlement => settlement.CreatedAtUtc)
            .Take(8)
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

        var branches = await _dbContext.VendorBranches
            .AsNoTracking()
            .Where(branch => branch.VendorId == vendorId)
            .Select(branch => new { branch.IsActive })
            .ToListAsync(cancellationToken);

        var offersStatePayload = await _dbContext.VendorWorkspaceStates
            .AsNoTracking()
            .Where(state => state.VendorId == vendorId && state.Feature == "offers")
            .Select(state => state.PayloadJson)
            .FirstOrDefaultAsync(cancellationToken);

        var offersState = ParseOffersWorkspaceState(offersStatePayload, now);

        var paidOrders = orders.Where(order =>
            order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Settled || order.Status == OrderStatus.Delivered).ToList();
        var previousPaidOrders = previousOrders.Where(order =>
            order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Settled || order.Status == OrderStatus.Delivered).ToList();

        var grossSales = orders.Sum(order => order.TotalAmount);
        var paidSales = paidOrders.Sum(order => order.TotalAmount);
        var previousPaidSales = previousPaidOrders.Sum(order => order.TotalAmount);
        var fees = paidOrders.Sum(order => order.CommissionAmount);
        var payoutsPaid = payouts.Where(payout => payout.Status == PayoutStatus.Paid).Sum(payout => payout.Amount);

        // Fetch wallet details instead of manually calculating
        var vendorWallet = await _dbContext.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.OwnerType == WalletOwnerType.Vendor && w.OwnerId == vendorId, cancellationToken);

        var vendorWalletTransactions = vendorWallet is null
            ? []
            : await _dbContext.WalletTransactions
                .AsNoTracking()
                .Where(txn => txn.WalletId == vendorWallet.Id)
                .OrderByDescending(txn => txn.CreatedAtUtc)
                .Select(txn => new
                {
                    txn.Id,
                    txn.TxnType,
                    txn.Direction,
                    txn.Amount,
                    txn.CreatedAtUtc,
                    txn.Description,
                    txn.ReferenceType,
                    txn.ReferenceId
                })
                .Take(24)
                .ToListAsync(cancellationToken);
            
        var vendorDetails = await _dbContext.Vendors
            .AsNoTracking()
            .Where(v => v.Id == vendorId)
            .Select(v => v.FinancialLifecycleMode)
            .FirstOrDefaultAsync(cancellationToken);

        var availableBalance = vendorWallet?.CurrentBalance ?? 0m;
        var pendingSettlement = settlements.Where(settlement => settlement.Status is SettlementStatus.Pending or SettlementStatus.Processing).Sum(settlement => settlement.NetAmount);
        var holdAmount = vendorWallet?.PendingBalance ?? 0m;
        
        var financialLifecycleModeStr = vendorDetails.ToString();
        var nextSettlementAt = CalculateNextSettlementDate(vendorDetails);

        var prepTimes = orders
            .Where(o => o.AcceptedAtUtc.HasValue && o.ReadyAtUtc.HasValue)
            .Select(o => (o.ReadyAtUtc!.Value - o.AcceptedAtUtc!.Value).TotalMinutes)
            .ToList();
        var averagePrepTimeMinutes = prepTimes.Count() > 0 ? (int)prepTimes.Average() : 0;
        var prepEfficiencyScore = prepTimes.Count() > 0 ? Math.Round((decimal)prepTimes.Count(t => t <= 30) / prepTimes.Count() * 100, 1) : 100m;

        var lostRevenueAmount = orders.Where(o => o.Status is OrderStatus.Cancelled or OrderStatus.VendorRejected).Sum(o => o.TotalAmount);

        var activeProductIdsThisPeriod = orderItems.Select(oi => oi.VendorProductId).Distinct().ToHashSet();
        var idleCapitalAmount = vendorProducts
            .Where(p => p.StockQuantity > 0 && !activeProductIdsThisPeriod.Contains(p.Id))
            .Sum(p => p.StockQuantity * p.SellingPrice);

        var ordersCount = orders.Count();
        var previousOrdersCount = previousOrders.Count();
        var averageOrderValue = paidOrders.Count() > 0 ? paidSales / paidOrders.Count() : 0m;
        var previousAverageOrderValue = previousPaidOrders.Count() > 0 ? previousPaidSales / previousPaidOrders.Count() : 0m;
        var cancelledCount = orders.Count(order => order.Status is OrderStatus.Cancelled or OrderStatus.VendorRejected);
        var refundedCount = orders.Count(order => order.PaymentStatus is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded || order.Status == OrderStatus.Refunded);
        var cancellationRate = ordersCount > 0 ? (decimal)cancelledCount / ordersCount : 0m;
        var refundRate = ordersCount > 0 ? (decimal)refundedCount / ordersCount : 0m;

        var pendingOrders = activeOrders.Count(order => order.Status is OrderStatus.Placed or OrderStatus.PendingVendorAcceptance or OrderStatus.Accepted or OrderStatus.Preparing);
        var readyForPickup = activeOrders.Count(order => order.Status == OrderStatus.ReadyForPickup);
        var lateOrders = activeOrders.Count(order => order.PlacedAtUtc <= now.AddHours(-24) && order.Status is not (OrderStatus.ReadyForPickup or OrderStatus.Delivered));
        var driverIssues = activeOrders.Count(order => order.Status is OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned);
        var openDisputes = disputes.Count(item => item.Status is not (OrderSupportCaseStatus.Rejected or OrderSupportCaseStatus.Resolved));
        var awaitingVendorResponse = disputes.Count(item => string.IsNullOrWhiteSpace(item.VendorResponse) && item.Status is not (OrderSupportCaseStatus.Rejected or OrderSupportCaseStatus.Resolved));
        var highPriorityDisputes = disputes.Count(item => item.Priority is OrderSupportCasePriority.High or OrderSupportCasePriority.Critical && item.Status is not (OrderSupportCaseStatus.Rejected or OrderSupportCaseStatus.Resolved));
        var refundRequests = disputes.Count(item => item.Type == OrderSupportCaseType.ReturnRequest);

        var activeProducts = vendorProducts.Count(product => product.IsAvailable);
        var inactiveProducts = vendorProducts.Count(product => !product.IsAvailable);
        var outOfStock = vendorProducts.Count(product => product.StockQuantity == 0);
        var lowStock = vendorProducts.Count(product => product.StockQuantity > 0 && product.StockQuantity <= 20);
        var lowStockCritical = vendorProducts.Count(product => product.StockQuantity > 0 && product.StockQuantity <= 5);
        var productsWithOffers = vendorProducts.Count(product => product.CompareAtPrice.HasValue && product.CompareAtPrice > product.SellingPrice);
        var offerCoverage = activeProducts > 0 ? (decimal)productsWithOffers / activeProducts : 0m;

        var totalReviews = reviews.Count;
        var averageRating = totalReviews > 0 ? (decimal)reviews.Sum(review => review.Rating) / totalReviews : 0m;
        var lowRatingCount = reviews.Count(review => review.Rating <= 2);
        var pendingReplies = reviews.Count(review => string.IsNullOrWhiteSpace(review.VendorReply));

        var alerts = BuildDashboardAlerts(lateOrders, lowStockCritical, awaitingVendorResponse, pendingReplies, pendingSettlement);
        var orderTrend = BuildTrendPoints(from, now, orders, item => item.PlacedAtUtc, items => items.Count(), items =>
            items.Where(order => order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Settled || order.Status == OrderStatus.Delivered).Sum(order => order.TotalAmount));
        var salesTrend = orderTrend.Select(point => new VendorDashboardDualTrendPointResponse(point.Label, point.SecondaryValue, point.Value)).ToList();

        var noMovementProducts = vendorProducts
            .Where(product => orderItems.All(item => item.VendorProductId != product.Id) && product.StockQuantity > 0)
            .OrderBy(product => product.StockQuantity)
            .Take(6)
            .Select(product => new VendorDashboardRankedItemResponse(
                product.Id.ToString(),
                product.NameAr,
                product.NameEn,
                product.StockQuantity,
                0))
            .ToList();

        var topProducts = orderItems
            .GroupBy(item => item.VendorProductId)
            .Select(group =>
            {
                var product = vendorProducts.FirstOrDefault(candidate => candidate.Id == group.Key);
                return new VendorDashboardRankedItemResponse(
                    group.Key.ToString(),
                    product?.NameAr ?? group.First().ProductName,
                    product?.NameEn ?? group.First().ProductName,
                    group.Sum(item => item.LineTotal),
                    group.Sum(item => item.Quantity));
            })
            .OrderByDescending(item => item.Metric)
            .Take(6)
            .ToList();

        return Ok(new VendorDashboardOverviewResponse(
            now,
            normalizedPeriod,
            new VendorDashboardHeroStatsResponse(pendingOrders, lateOrders, readyForPickup, driverIssues, openDisputes, lowStockCritical),
            new VendorDashboardOrdersSectionResponse(
                pendingOrders,
                lateOrders,
                readyForPickup,
                driverIssues,
                openDisputes,
                lowStockCritical,
                prepEfficiencyScore,
                averagePrepTimeMinutes,
                orderTrend.Select(point => new VendorDashboardDualTrendPointResponse(point.Label, point.Value, point.SecondaryValue)).ToList(),
                activeOrders.GroupBy(order => order.Status.ToString()).Select(group => new VendorDashboardBreakdownSliceResponse(group.Key, group.Key, group.Count())).OrderByDescending(group => group.Value).ToList(),
                [
                    new VendorDashboardBreakdownSliceResponse("new", "NEW", orders.Count(order => order.Status is OrderStatus.Placed or OrderStatus.PendingVendorAcceptance)),
                    new VendorDashboardBreakdownSliceResponse("confirmed", "CONFIRMED", orders.Count(order => order.Status == OrderStatus.Accepted)),
                    new VendorDashboardBreakdownSliceResponse("preparing", "PREPARING", orders.Count(order => order.Status == OrderStatus.Preparing)),
                    new VendorDashboardBreakdownSliceResponse("ready", "READY_FOR_PICKUP", orders.Count(order => order.Status == OrderStatus.ReadyForPickup)),
                    new VendorDashboardBreakdownSliceResponse("delivered", "DELIVERED", orders.Count(order => order.Status == OrderStatus.Delivered)),
                    new VendorDashboardBreakdownSliceResponse("cancelled", "CANCELLED", orders.Count(order => order.Status is OrderStatus.Cancelled or OrderStatus.VendorRejected))
                ],
                activeOrders
                    .Where(order => order.PlacedAtUtc <= now.AddHours(-24) || order.Status is OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned)
                    .OrderBy(order => order.PlacedAtUtc)
                    .Take(6)
                    .Select(order => new VendorDashboardUrgentOrderResponse(
                        order.Id.ToString(),
                        order.OrderNumber,
                        order.Status.ToString(),
                        order.PlacedAtUtc,
                        order.Status is OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned ? "driver_follow_up" : "late_order"))
                    .ToList(),
                alerts.Where(alert => alert.Domain == "operations").Take(6).ToList()),
            new VendorDashboardSalesSectionEnvelopeResponse(
                new VendorDashboardSalesSectionResponse(
                    grossSales,
                    paidSales,
                    ordersCount,
                    averageOrderValue,
                    cancellationRate,
                    refundRate,
                    lostRevenueAmount,
                    salesTrend,
                    orders
                        .GroupBy(order => (int)order.PlacedAtUtc.DayOfWeek)
                        .OrderBy(group => group.Key)
                        .Select(group => new VendorDashboardBreakdownSliceResponse(
                            group.Key.ToString(CultureInfo.InvariantCulture),
                            group.Key.ToString(CultureInfo.InvariantCulture),
                            group.Count()))
                        .ToList(),
                    topProducts,
                    orderItems.GroupBy(item => new { item.CategoryAr, item.CategoryEn }).Select(group => new VendorDashboardBreakdownSliceResponse(group.Key.CategoryEn ?? group.Key.CategoryAr ?? "category", group.Key.CategoryEn ?? group.Key.CategoryAr ?? "category", group.Sum(item => item.Quantity))).OrderByDescending(group => group.Value).Take(6).ToList(),
                    noMovementProducts),
                new VendorDashboardDeltaSummaryResponse(
                    CalculateDelta(paidSales, previousPaidSales),
                    CalculateDelta(ordersCount, previousOrdersCount),
                    CalculateDelta(averageOrderValue, previousAverageOrderValue))),
            new VendorDashboardInventorySectionResponse(
                activeProducts,
                outOfStock,
                lowStock,
                inactiveProducts,
                productsWithOffers,
                idleCapitalAmount,
                [
                    new VendorDashboardBreakdownSliceResponse("healthy", "healthy", vendorProducts.Count(product => product.StockQuantity > 20)),
                    new VendorDashboardBreakdownSliceResponse("low", "low", lowStock),
                    new VendorDashboardBreakdownSliceResponse("out", "out", outOfStock)
                ],
                vendorProducts.Where(product => product.StockQuantity <= 20).OrderBy(product => product.StockQuantity).Take(6).Select(product => new VendorDashboardRankedItemResponse(product.Id.ToString(), product.NameAr, product.NameEn, product.StockQuantity, product.StockQuantity <= 5 ? 3 : product.StockQuantity <= 10 ? 2 : 1)).ToList(),
                BuildTrendPoints(from, now, vendorProducts, item => item.CreatedAtUtc, items => items.Count(), items => items.Count()).Select(point => new VendorDashboardTrendPointResponse(point.Label, point.Value)).ToList(),
                vendorProducts.Where(product => product.StockQuantity <= 5).OrderBy(product => product.StockQuantity).Take(6).Select(product => new VendorDashboardRankedItemResponse(product.Id.ToString(), product.NameAr, product.NameEn, product.StockQuantity, 0)).ToList(),
                noMovementProducts),
            new VendorDashboardOffersSectionResponse(
                productsWithOffers,
                offersState.ClearanceOffersCount,
                offersState.ExpiringOffersCount,
                offerCoverage,
                [
                    new VendorDashboardBreakdownSliceResponse("direct", "direct", productsWithOffers),
                    new VendorDashboardBreakdownSliceResponse("coupons", "coupons", offersState.CouponsCount),
                    new VendorDashboardBreakdownSliceResponse("category_campaigns", "category_campaigns", offersState.CategoryCampaignsCount),
                    new VendorDashboardBreakdownSliceResponse("clearance", "clearance", offersState.ClearanceOffersCount)
                ],
                BuildDiscountBandSlices(vendorProducts),
                [
                    new VendorDashboardBreakdownSliceResponse("direct", "direct", productsWithOffers),
                    new VendorDashboardBreakdownSliceResponse("clearance", "clearance", offersState.ClearanceOffersCount),
                    new VendorDashboardBreakdownSliceResponse("campaigns", "campaigns", offersState.CategoryCampaignsCount),
                    new VendorDashboardBreakdownSliceResponse("coupons", "coupons", offersState.CouponsCount)
                ],
                vendorProducts.Where(product => product.CompareAtPrice.HasValue && product.CompareAtPrice > product.SellingPrice && product.StockQuantity > 0).OrderBy(product => product.StockQuantity).Take(6).Select(product => new VendorDashboardRankedItemResponse(product.Id.ToString(), product.NameAr, product.NameEn, product.CompareAtPrice.GetValueOrDefault() - product.SellingPrice, product.StockQuantity)).ToList(),
                vendorProducts.Where(product => product.StockQuantity > 0 && product.StockQuantity <= 12 && !(product.CompareAtPrice.HasValue && product.CompareAtPrice > product.SellingPrice)).OrderBy(product => product.StockQuantity).Take(6).Select(product => new VendorDashboardRankedItemResponse(product.Id.ToString(), product.NameAr, product.NameEn, product.StockQuantity, 0)).ToList()),
            new VendorDashboardReviewsSectionResponse(
                averageRating,
                totalReviews,
                lowRatingCount,
                pendingReplies,
                0,
                Enumerable.Range(1, 5)
                    .Select(star => new VendorDashboardBreakdownSliceResponse(
                        star.ToString(CultureInfo.InvariantCulture),
                        star.ToString(CultureInfo.InvariantCulture),
                        reviews.Count(review => review.Rating == star)))
                    .ToList(),
                BuildTrendPoints(from, now, reviews.Where(review => review.CreatedAtUtc >= from).ToList(), item => item.CreatedAtUtc, items => items.Count(), items => items.Count()).Select(point => new VendorDashboardTrendPointResponse(point.Label, point.Value)).ToList(),
                [
                    new VendorDashboardBreakdownSliceResponse("replied", "replied", reviews.Count(review => !string.IsNullOrWhiteSpace(review.VendorReply))),
                    new VendorDashboardBreakdownSliceResponse("pending", "pending", pendingReplies)
                ],
                reviews.Where(review => review.Rating <= 2 && string.IsNullOrWhiteSpace(review.VendorReply)).OrderByDescending(review => review.CreatedAtUtc).Take(6).Select(review => new VendorDashboardReviewListItemResponse(review.Id.ToString(), review.CustomerName, review.Rating, review.Comment ?? string.Empty, review.CreatedAtUtc, true)).ToList(),
                reviews.OrderByDescending(review => review.CreatedAtUtc).Take(6).Select(review => new VendorDashboardReviewListItemResponse(review.Id.ToString(), review.CustomerName, review.Rating, review.Comment ?? string.Empty, review.CreatedAtUtc, !string.IsNullOrWhiteSpace(review.VendorReply))).ToList()),
            new VendorDashboardFinanceSectionResponse(
                availableBalance,
                pendingSettlement,
                paidSales - fees,
                fees,
                payoutsPaid,
                holdAmount,
                nextSettlementAt,
                financialLifecycleModeStr,
                BuildFinanceTrend(paidOrders, payouts).Select(point => new VendorDashboardDualTrendPointResponse(point.Label, point.Sales, point.Payout)).ToList(),
                settlements.GroupBy(settlement => MapSettlementStatus(settlement.Status)).Select(group => new VendorDashboardBreakdownSliceResponse(group.Key, group.Key, group.Count())).ToList(),
                vendorWalletTransactions
                    .GroupBy(txn => MapLedgerEntryType(txn.TxnType))
                    .Select(group => new VendorDashboardBreakdownSliceResponse(group.Key, group.Key, group.Count()))
                    .OrderByDescending(group => group.Value)
                    .ToList(),
                settlements.Select(settlement => new VendorDashboardSettlementListItemResponse(settlement.Id.ToString(), $"SET-{settlement.CreatedAtUtc:yyMMdd}", settlement.NetAmount, MapSettlementStatus(settlement.Status), settlement.CreatedAtUtc, settlement.OrdersCount)).ToList(),
                vendorWalletTransactions
                    .Select(txn => MapDashboardLedgerEntry(txn.Id, txn.TxnType, txn.Direction, txn.Amount, txn.CreatedAtUtc, txn.Description, txn.ReferenceType, txn.ReferenceId))
                    .OrderByDescending(item => item.OccurredAtUtc)
                    .Take(8)
                    .ToList()),
            new VendorDashboardDisputesSectionResponse(
                openDisputes,
                highPriorityDisputes,
                refundRequests,
                awaitingVendorResponse,
                disputes.GroupBy(item => item.Status.ToString()).Select(group => new VendorDashboardBreakdownSliceResponse(group.Key, group.Key, group.Count())).OrderByDescending(group => group.Value).ToList(),
                disputes.GroupBy(item => item.Type.ToString()).Select(group => new VendorDashboardBreakdownSliceResponse(group.Key, group.Key, group.Count())).OrderByDescending(group => group.Value).ToList(),
                BuildTrendPoints(from, now, disputes.Where(item => item.CreatedAtUtc >= from).ToList(), item => item.CreatedAtUtc, items => items.Count(), items => items.Count()).Select(point => new VendorDashboardTrendPointResponse(point.Label, point.Value)).ToList(),
                disputes.Where(item => string.IsNullOrWhiteSpace(item.VendorResponse) && item.Status is not (OrderSupportCaseStatus.Rejected or OrderSupportCaseStatus.Resolved)).OrderByDescending(item => item.Priority).ThenBy(item => item.CreatedAtUtc).Take(6).Select(item => new VendorDashboardDisputeListItemResponse(item.Id.ToString(), item.Type.ToString(), item.Status.ToString(), item.Priority.ToString(), item.Message, item.CreatedAtUtc)).ToList(),
                disputes.Where(item => item.Priority is OrderSupportCasePriority.High or OrderSupportCasePriority.Critical).OrderByDescending(item => item.UpdatedAtUtc).Take(6).Select(item => new VendorDashboardDisputeListItemResponse(item.Id.ToString(), item.Type.ToString(), item.Status.ToString(), item.Priority.ToString(), item.Message, item.UpdatedAtUtc)).ToList()),
            new VendorDashboardStaffSectionResponse(
                branches.Count(branch => branch.IsActive),
                0,
                0,
                branches.Count(branch => !branch.IsActive),
                [
                    new VendorDashboardBreakdownSliceResponse("active", "active", branches.Count(branch => branch.IsActive)),
                    new VendorDashboardBreakdownSliceResponse("inactive", "inactive", branches.Count(branch => !branch.IsActive))
                ],
                []),
            alerts));
    }

    [HttpGet("dashboard/overview-legacy")]
    public async Task<ActionResult<VendorDashboardOverview>> GetDashboardOverviewLegacy(
        [FromQuery] string period = "7d",
        CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        
        var now = DateTime.UtcNow;
        var (from, periodDays) = period.ToLowerInvariant() switch
        {
            "today" => (now.Date, 1),
            "30d" => (now.AddDays(-30), 30),
            _ => (now.AddDays(-7), 7)
        };
        var prevFrom = from.AddDays(-periodDays);
        var prevTo = from;

        // Orders within period
        var ordersQuery = _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.VendorId == vendorId && o.PlacedAtUtc >= from);
            
        var ordersCount = await ordersQuery.CountAsync(cancellationToken);
        
        var orders = await ordersQuery
            .Select(o => new { o.Id, o.OrderNumber, o.Status, o.PaymentStatus, o.TotalAmount, o.PlacedAtUtc })
            .ToListAsync(cancellationToken);

        // All active orders (not delivered/cancelled/rejected) regardless of period for operational summary
        var activeOrders = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.VendorId == vendorId && 
                        o.Status != OrderStatus.Delivered && 
                        o.Status != OrderStatus.Cancelled &&
                        o.Status != OrderStatus.VendorRejected)
            .Select(o => new { o.Id, o.OrderNumber, o.Status, o.PlacedAtUtc })
            .ToListAsync(cancellationToken);

        var pendingOrdersCount = activeOrders.Count(o => o.Status is OrderStatus.Placed or OrderStatus.PendingVendorAcceptance);
        var readyForPickupCount = activeOrders.Count(o => o.Status == OrderStatus.ReadyForPickup);
        var lateOrdersCount = activeOrders.Count(o => o.PlacedAtUtc < now.AddHours(-24) && o.Status != OrderStatus.ReadyForPickup);

        var lowStockCount = await _dbContext.VendorProducts
            .AsNoTracking()
            .CountAsync(p => p.VendorId == vendorId && p.StockQuantity > 0 && p.StockQuantity <= 5, cancellationToken);
            
        var activeProducts = await _dbContext.VendorProducts
            .AsNoTracking()
            .CountAsync(p => p.VendorId == vendorId && p.IsAvailable, cancellationToken);
            
        var activeOffers = await _dbContext.VendorProducts
            .AsNoTracking()
            .CountAsync(p => p.VendorId == vendorId && p.IsAvailable && p.CompareAtPrice > p.SellingPrice, cancellationToken);

        var openDisputesCount = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .Where(c => c.Order.VendorId == vendorId && c.Status != OrderSupportCaseStatus.Rejected && c.Status != OrderSupportCaseStatus.Resolved)
            .CountAsync(cancellationToken);

        var unrepliedLowReviewsCount = await _dbContext.Reviews
            .AsNoTracking()
            .Where(r => r.VendorId == vendorId && r.Rating <= 3 && string.IsNullOrWhiteSpace(r.VendorReply))
            .CountAsync(cancellationToken);

        var paidOrders = orders.Where(o => o.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Settled || o.Status == OrderStatus.Delivered).ToList();
        var paidSales = paidOrders.Sum(o => o.TotalAmount);
        var averageOrderValue = paidOrders.Any() ? paidSales / paidOrders.Count : 0;
        
        var cancelledCount = orders.Count(o => o.Status is OrderStatus.Cancelled or OrderStatus.VendorRejected);
        var cancellationRate = ordersCount > 0 ? (decimal)cancelledCount / ordersCount : 0;

        // Grouping for charts
        var orderStatusBreakdown = activeOrders
            .GroupBy(o => o.Status)
            .Select(g => new VendorDashboardStatusSlice(g.Key.ToString(), g.Count()))
            .ToList();

        var salesTrend = orders
            .GroupBy(o => o.PlacedAtUtc.Date)
            .OrderBy(g => g.Key)
            .Select(g => new VendorDashboardTrendPoint(
                g.Key.ToString("yyyy-MM-dd"),
                g.Where(o => o.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Settled || o.Status == OrderStatus.Delivered).Sum(o => o.TotalAmount),
                g.Count()))
            .ToList();

        // Previous period KPIs for delta comparison
        var prevOrders = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.VendorId == vendorId && o.PlacedAtUtc >= prevFrom && o.PlacedAtUtc < prevTo)
            .Select(o => new { o.Status, o.PaymentStatus, o.TotalAmount })
            .ToListAsync(cancellationToken);
        var prevPaidOrders = prevOrders.Where(o => o.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Settled || o.Status == OrderStatus.Delivered).ToList();
        var prevPaidSales = prevPaidOrders.Sum(o => o.TotalAmount);
        var prevOrdersCount = prevOrders.Count;
        var prevAov = prevPaidOrders.Count > 0 ? prevPaidSales / prevPaidOrders.Count : 0;

        // Rating summary
        var allRatings = await _dbContext.Reviews
            .AsNoTracking()
            .Where(r => r.VendorId == vendorId)
            .Select(r => r.Rating)
            .ToListAsync(cancellationToken);
        var totalReviews = allRatings.Count;
        var avgRating = totalReviews > 0 ? (decimal)allRatings.Sum() / totalReviews : 0;
        var ratingDistribution = Enumerable.Range(1, 5)
            .Select(star => new VendorDashboardRatingStar(star, allRatings.Count(r => r == star)))
            .ToList();
        var ratingsSummary = new VendorDashboardRatingSummary(avgRating, totalReviews, ratingDistribution);

        // Acceptance rate
        var totalReceived = orders.Count(o => o.Status != OrderStatus.PendingPayment);
        var accepted = orders.Count(o => o.Status != OrderStatus.VendorRejected && o.Status != OrderStatus.PendingPayment);
        var acceptanceRate = totalReceived > 0 ? (decimal)accepted / totalReceived : 1;

        // Recent completed orders
        var recentCompleted = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.VendorId == vendorId && o.Status == OrderStatus.Delivered)
            .OrderByDescending(o => o.PlacedAtUtc)
            .Take(8)
            .Select(o => new VendorDashboardCompletedOrder(o.Id.ToString(), o.OrderNumber, o.TotalAmount, o.PlacedAtUtc))
            .ToListAsync(cancellationToken);

        // Top products by order count in this period
        var topProductIds = await _dbContext.OrderItems
            .AsNoTracking()
            .Where(i => i.Order.VendorId == vendorId && i.Order.PlacedAtUtc >= from)
            .GroupBy(i => i.VendorProductId)
            .OrderByDescending(g => g.Sum(i => i.Quantity))
            .Take(10)
            .Select(g => new { VendorProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToListAsync(cancellationToken);
            
        var productDetails = await _dbContext.VendorProducts
            .AsNoTracking()
            .Where(p => topProductIds.Select(t => t.VendorProductId).Contains(p.Id))
            .Select(p => new { p.Id, p.MasterProduct.NameAr, p.MasterProduct.NameEn, p.SellingPrice, p.StockQuantity })
            .ToListAsync(cancellationToken);

        var topProducts = topProductIds.Select(t => 
        {
            var p = productDetails.FirstOrDefault(pd => pd.Id == t.VendorProductId);
            return new VendorDashboardTopProduct(
                t.VendorProductId.ToString(),
                p?.NameAr ?? "منتج",
                p?.NameEn ?? "Product",
                p?.SellingPrice ?? 0,
                t.Quantity,
                p?.StockQuantity ?? 0);
        }).ToList();

        var urgentOrders = activeOrders
            .Where(o => o.PlacedAtUtc < now.AddHours(-24))
            .OrderBy(o => o.PlacedAtUtc)
            .Take(5)
            .Select(o => new VendorDashboardUrgentOrder(
                o.Id.ToString(),
                o.OrderNumber,
                o.Status.ToString(),
                o.PlacedAtUtc,
                "متأخر"))
            .ToList();

        var inventoryWatchlist = await _dbContext.VendorProducts
            .AsNoTracking()
            .Where(p => p.VendorId == vendorId && p.StockQuantity > 0 && p.StockQuantity <= 5)
            .OrderBy(p => p.StockQuantity)
            .Take(5)
            .Select(p => new VendorDashboardInventoryItem(
                p.Id.ToString(),
                p.MasterProduct.NameAr,
                p.MasterProduct.NameEn,
                p.StockQuantity,
                "low_stock"))
            .ToListAsync(cancellationToken);

        // Finance Snapshot
        var settlements = await _dbContext.Settlements
            .AsNoTracking()
            .Where(s => s.VendorId == vendorId && (s.Status == SettlementStatus.Pending || s.Status == SettlementStatus.Processing))
            .ToListAsync(cancellationToken);
            
        var pendingSettlement = settlements.Sum(s => s.NetAmount);
        
        var nextPayoutDate = settlements
            .OrderBy(s => s.CreatedAtUtc)
            .Select(s => s.ProcessedAtUtc ?? s.CreatedAtUtc.AddDays(7))
            .FirstOrDefault();
            
        var financeSnapshot = new VendorDashboardFinanceSnapshot(
            paidSales, // basic representation of available balance for dashboard
            pendingSettlement,
            nextPayoutDate == default ? now.AddDays(7) : nextPayoutDate);

        var alerts = new List<VendorDashboardAlert>();
        if (lateOrdersCount > 0)
        {
            alerts.Add(new VendorDashboardAlert(
                "high",
                "DASHBOARD.ALERTS.LATE_ORDERS_TITLE",
                "DASHBOARD.ALERTS.LATE_ORDERS_BODY",
                "/orders",
                new { lateState = "LATE" }));
        }
        if (pendingSettlement > 10000)
        {
             alerts.Add(new VendorDashboardAlert(
                "medium",
                "DASHBOARD.ALERTS.LARGE_SETTLEMENT_TITLE",
                "DASHBOARD.ALERTS.LARGE_SETTLEMENT_BODY",
                "/finance",
                new { tab = "settlements" }));
        }

        var kpiDeltas = new VendorDashboardKpiDeltas(
            paidSales > 0 && prevPaidSales > 0 ? (paidSales - prevPaidSales) / prevPaidSales : 0,
            ordersCount > 0 && prevOrdersCount > 0 ? (decimal)(ordersCount - prevOrdersCount) / prevOrdersCount : 0,
            averageOrderValue > 0 && prevAov > 0 ? (averageOrderValue - prevAov) / prevAov : 0);

        var response = new VendorDashboardOverview(
            now,
            period,
            new VendorDashboardSummary(pendingOrdersCount, readyForPickupCount, lateOrdersCount, lowStockCount, openDisputesCount, unrepliedLowReviewsCount),
            new VendorDashboardKpi(paidSales, ordersCount, averageOrderValue, activeProducts, activeOffers, cancellationRate, acceptanceRate),
            kpiDeltas,
            salesTrend,
            orderStatusBreakdown,
            topProducts,
            urgentOrders,
            inventoryWatchlist,
            financeSnapshot,
            ratingsSummary,
            recentCompleted,
            alerts);

        return Ok(response);
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

        var vendorFinancialMode = await _dbContext.Vendors
            .AsNoTracking()
            .Where(vendor => vendor.Id == vendorId)
            .Select(vendor => vendor.FinancialLifecycleMode)
            .FirstOrDefaultAsync(cancellationToken);

        var vendorWallet = await _dbContext.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(wallet => wallet.OwnerType == WalletOwnerType.Vendor && wallet.OwnerId == vendorId, cancellationToken);

        var vendorWalletTransactions = vendorWallet is null
            ? []
            : await _dbContext.WalletTransactions
                .AsNoTracking()
                .Where(txn => txn.WalletId == vendorWallet.Id)
                .OrderByDescending(txn => txn.CreatedAtUtc)
                .Select(txn => new
                {
                    txn.Id,
                    txn.TxnType,
                    txn.Direction,
                    txn.Amount,
                    txn.CreatedAtUtc,
                    txn.Description,
                    txn.ReferenceType,
                    txn.ReferenceId
                })
                .Take(40)
                .ToListAsync(cancellationToken);

        var paidOrders = orders.Where(order =>
            order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Settled || order.Status == OrderStatus.Delivered).ToList();
        var netSales = paidOrders.Sum(order => order.TotalAmount);
        var fees = paidOrders.Sum(order => order.CommissionAmount);
        var payoutsPaid = payouts.Where(payout => payout.Status == PayoutStatus.Paid).Sum(payout => payout.Amount);
        var pendingSettlement = settlements
            .Where(settlement => settlement.Status is SettlementStatus.Pending or SettlementStatus.Processing)
            .Sum(settlement => settlement.NetAmount);
        var availableBalance = vendorWallet?.CurrentBalance ?? 0m;
        var holdAmount = vendorWallet?.PendingBalance ?? 0m;

        var trend = BuildFinanceTrend(paidOrders, payouts);
        var ledger = vendorWalletTransactions
            .OrderByDescending(txn => txn.CreatedAtUtc)
            .Select(txn => MapFinanceLedgerEntry(txn.Id, txn.TxnType, txn.Direction, txn.Amount, txn.CreatedAtUtc, txn.Description, txn.ReferenceType, txn.ReferenceId))
            .Take(10)
            .ToList();

        var nextPayoutDate = ResolveNextPayoutDate(vendorFinancialMode, settlements);

        return Ok(new VendorFinanceSnapshotResponse(
            availableBalance,
            pendingSettlement,
            nextPayoutDate.ToString("yyyy-MM-dd"),
            primaryBank is null ? "Bank transfer" : $"Bank Transfer - {primaryBank.BankName}",
            holdAmount,
            vendorFinancialMode.ToString(),
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

        var vendorWallet = await _dbContext.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(wallet => wallet.OwnerType == WalletOwnerType.Vendor && wallet.OwnerId == vendorId, cancellationToken);

        if (vendorWallet is null)
        {
            return Ok(new VendorFinanceLedgerPageResponse(new List<VendorLedgerEntryResponse>(), page, pageSize, 0, 0));
        }

        var query = _dbContext.WalletTransactions
            .AsNoTracking()
            .Where(txn => txn.WalletId == vendorWallet.Id)
            .OrderByDescending(txn => txn.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var transactions = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(txn => new
            {
                txn.Id,
                txn.TxnType,
                txn.Direction,
                txn.Amount,
                txn.CreatedAtUtc,
                txn.Description,
                txn.ReferenceType,
                txn.ReferenceId
            })
            .ToListAsync(cancellationToken);

        var items = transactions
            .Select(txn => MapFinanceLedgerEntry(txn.Id, txn.TxnType, txn.Direction, txn.Amount, txn.CreatedAtUtc, txn.Description, txn.ReferenceType, txn.ReferenceId))
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

    private static (string NormalizedPeriod, DateTime From, DateTime PreviousFrom) ResolveDashboardPeriod(string period, DateTime now)
    {
        return period.ToLowerInvariant() switch
        {
            "today" => ("today", now.Date, now.Date.AddDays(-1)),
            "30d" => ("30d", now.Date.AddDays(-29), now.Date.AddDays(-59)),
            "90d" => ("90d", now.Date.AddDays(-89), now.Date.AddDays(-179)),
            _ => ("7d", now.Date.AddDays(-6), now.Date.AddDays(-13))
        };
    }

    private static DateTime? CalculateNextSettlementDate(VendorFinancialLifecycleMode mode)
    {
        var now = DateTime.UtcNow.Date;
        return mode switch
        {
            VendorFinancialLifecycleMode.PerOrderDirectPayout => null,
            VendorFinancialLifecycleMode.Weekly => now.AddDays((7 - (int)now.DayOfWeek) % 7), // Sunday (assuming 0 is Sunday, so if today is Sunday, adds 0 or 7 depending on logic. Let's just say end of week)
            VendorFinancialLifecycleMode.Biweekly => now.Day <= 15 ? new DateTime(now.Year, now.Month, 16) : new DateTime(now.Year, now.Month, 1).AddMonths(1),
            VendorFinancialLifecycleMode.Monthly => new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month)),
            _ => null
        };
    }

    private static DateTime ResolveNextPayoutDate(
        VendorFinancialLifecycleMode mode,
        IReadOnlyCollection<dynamic> settlements)
    {
        var pendingDate = settlements
            .Where(settlement => settlement.Status is SettlementStatus.Pending or SettlementStatus.Processing)
            .OrderBy(settlement => settlement.CreatedAtUtc)
            .Select(settlement => (DateTime?)(settlement.ProcessedAtUtc ?? settlement.CreatedAtUtc))
            .FirstOrDefault();

        if (pendingDate.HasValue)
        {
            return pendingDate.Value;
        }

        return mode == VendorFinancialLifecycleMode.PerOrderDirectPayout
            ? DateTime.UtcNow.Date
            : CalculateNextSettlementDate(mode) ?? DateTime.UtcNow.Date;
    }

    private static string MapLedgerEntryType(WalletTxnType txnType) =>
        txnType switch
        {
            WalletTxnType.OrderRevenue => "sales",
            WalletTxnType.Refund => "refunds",
            WalletTxnType.Payout or WalletTxnType.Hold or WalletTxnType.Release => "payouts",
            _ => "adjustments"
        };

    private static VendorDashboardLedgerListItemResponse MapDashboardLedgerEntry(
        Guid id,
        WalletTxnType txnType,
        string direction,
        decimal amount,
        DateTime occurredAtUtc,
        string? description,
        string? referenceType,
        Guid? referenceId)
    {
        var type = MapLedgerEntryType(txnType) switch
        {
            "sales" => "sale",
            "refunds" => "refund",
            "payouts" => "payout",
            _ => "adjustment"
        };

        var label = string.IsNullOrWhiteSpace(description)
            ? txnType switch
            {
                WalletTxnType.OrderRevenue => "Order revenue",
                WalletTxnType.Hold => "Payout hold",
                WalletTxnType.Release => "Payout hold release",
                WalletTxnType.Payout => "Vendor payout",
                WalletTxnType.Refund => "Refund",
                _ => "Wallet adjustment"
            }
            : description.Trim();

        var reference = !string.IsNullOrWhiteSpace(referenceType)
            ? referenceType.Trim()
            : referenceId?.ToString() ?? id.ToString();

        return new VendorDashboardLedgerListItemResponse(
            id.ToString(),
            type,
            label,
            amount,
            direction.Equals("IN", StringComparison.OrdinalIgnoreCase) ? "in" : "out",
            occurredAtUtc,
            reference);
    }

    private static VendorLedgerEntryResponse MapFinanceLedgerEntry(
        Guid id,
        WalletTxnType txnType,
        string direction,
        decimal amount,
        DateTime occurredAtUtc,
        string? description,
        string? referenceType,
        Guid? referenceId)
    {
        var type = MapLedgerEntryType(txnType) switch
        {
            "sales" => "sale",
            "refunds" => "refund",
            "payouts" => "payout",
            _ => "fee"
        };

        var (titleAr, titleEn) = ResolveFinanceLedgerTitles(txnType, description);
        var reference = !string.IsNullOrWhiteSpace(referenceType)
            ? referenceType.Trim()
            : referenceId?.ToString() ?? id.ToString();

        return new VendorLedgerEntryResponse(
            id.ToString(),
            occurredAtUtc.ToString("yyyy-MM-dd"),
            titleAr,
            titleEn,
            type,
            amount,
            direction.Equals("IN", StringComparison.OrdinalIgnoreCase) ? "in" : "out",
            reference);
    }

    private static (string TitleAr, string TitleEn) ResolveFinanceLedgerTitles(WalletTxnType txnType, string? description)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            var normalized = description.Trim();
            return (normalized, normalized);
        }

        return txnType switch
        {
            WalletTxnType.OrderRevenue => ("مستحق طلب", "Order revenue"),
            WalletTxnType.Hold => ("حجز للتحويل", "Payout hold"),
            WalletTxnType.Release => ("فك حجز التحويل", "Payout hold release"),
            WalletTxnType.Payout => ("تحويل بنكي", "Vendor payout"),
            WalletTxnType.Refund => ("استرجاع", "Refund"),
            WalletTxnType.Credit => ("إضافة للمحفظة", "Wallet credit"),
            WalletTxnType.Debit => ("خصم من المحفظة", "Wallet debit"),
            WalletTxnType.Settlement => ("تسوية", "Settlement"),
            WalletTxnType.CashCollected => ("تحصيل نقدي", "Cash collected"),
            _ => ("تسوية مالية", "Wallet adjustment")
        };
    }

    private static decimal CalculateDelta(decimal current, decimal previous)
    {
        if (previous == 0)
        {
            return current == 0 ? 0 : 1;
        }

        return (current - previous) / previous;
    }

    private static decimal CalculateDelta(int current, int previous)
    {
        if (previous == 0)
        {
            return current == 0 ? 0 : 1;
        }

        return (decimal)(current - previous) / previous;
    }

    private static List<VendorDashboardTrendBucket> BuildTrendPoints<T>(
        DateTime from,
        DateTime to,
        IReadOnlyCollection<T> source,
        Func<T, DateTime> dateSelector,
        Func<List<T>, int> primarySelector,
        Func<List<T>, decimal> secondarySelector)
    {
        var buckets = new List<VendorDashboardTrendBucket>();
        var totalDays = Math.Max(1, (to.Date - from.Date).Days + 1);

        for (var offset = 0; offset < totalDays; offset++)
        {
            var day = from.Date.AddDays(offset);
            var items = source.Where(item => dateSelector(item).Date == day).ToList();
            buckets.Add(new VendorDashboardTrendBucket(
                day.ToString("yyyy-MM-dd"),
                primarySelector(items),
                secondarySelector(items)));
        }

        return buckets;
    }

    private static List<VendorDashboardBreakdownSliceResponse> BuildDiscountBandSlices(IEnumerable<dynamic> products)
    {
        var slices = new Dictionary<string, int>
        {
            ["0-9"] = 0,
            ["10-19"] = 0,
            ["20-29"] = 0,
            ["30+"] = 0
        };

        foreach (var product in products)
        {
            decimal? compareAt = product.CompareAtPrice;
            decimal sellingPrice = product.SellingPrice;
            if (!compareAt.HasValue || compareAt <= sellingPrice || compareAt <= 0)
            {
                slices["0-9"]++;
                continue;
            }

            var discount = (compareAt.Value - sellingPrice) / compareAt.Value * 100m;
            if (discount >= 30)
            {
                slices["30+"]++;
            }
            else if (discount >= 20)
            {
                slices["20-29"]++;
            }
            else if (discount >= 10)
            {
                slices["10-19"]++;
            }
            else
            {
                slices["0-9"]++;
            }
        }

        return slices.Select(item => new VendorDashboardBreakdownSliceResponse(item.Key, item.Key, item.Value)).ToList();
    }

    private static List<VendorDashboardAlertItemResponse> BuildDashboardAlerts(
        int lateOrders,
        int lowStockCritical,
        int awaitingVendorResponse,
        int pendingReplies,
        decimal pendingSettlement)
    {
        var alerts = new List<VendorDashboardAlertItemResponse>();

        if (lateOrders > 0)
        {
            alerts.Add(new VendorDashboardAlertItemResponse(
                "operations-late-orders",
                "operations",
                "critical",
                "DASHBOARD.ALERTS.LATE_ORDERS_TITLE",
                "DASHBOARD.ALERTS.LATE_ORDERS_BODY",
                "/orders",
                BuildRouteQuery(("lateState", "LATE"))));
        }

        if (lowStockCritical > 0)
        {
            alerts.Add(new VendorDashboardAlertItemResponse(
                "inventory-low-stock",
                "inventory",
                "warning",
                "DASHBOARD.ALERTS.LOW_STOCK_TITLE",
                "DASHBOARD.ALERTS.LOW_STOCK_BODY",
                "/products",
                BuildRouteQuery(("stockState", "low"))));
        }

        if (awaitingVendorResponse > 0)
        {
            alerts.Add(new VendorDashboardAlertItemResponse(
                "risk-vendor-response",
                "risk",
                "warning",
                "DASHBOARD.ALERTS.DISPUTES_TITLE",
                "DASHBOARD.ALERTS.DISPUTES_BODY",
                "/disputes",
                BuildRouteQuery(("status", "submitted"))));
        }

        if (pendingReplies > 0)
        {
            alerts.Add(new VendorDashboardAlertItemResponse(
                "reviews-pending-replies",
                "reviews",
                "info",
                "DASHBOARD.ALERTS.REVIEWS_TITLE",
                "DASHBOARD.ALERTS.REVIEWS_BODY",
                "/reviews",
                BuildRouteQuery(("attention", "needs_attention"))));
        }

        if (pendingSettlement > 0)
        {
            alerts.Add(new VendorDashboardAlertItemResponse(
                "finance-pending-settlement",
                "finance",
                "info",
                "DASHBOARD.ALERTS.LARGE_SETTLEMENT_TITLE",
                "DASHBOARD.ALERTS.LARGE_SETTLEMENT_BODY",
                "/finance",
                BuildRouteQuery(("period", "month"))));
        }

        return alerts;
    }

    private static Dictionary<string, string> BuildRouteQuery(params (string Key, string Value)[] pairs)
    {
        return pairs.ToDictionary(item => item.Key, item => item.Value);
    }

    private static VendorDashboardOffersWorkspaceSnapshot ParseOffersWorkspaceState(string? payloadJson, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new VendorDashboardOffersWorkspaceSnapshot(0, 0, 0, 0);
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;
            var couponsCount = CountArray(root, "coupons");
            var campaignsCount = CountArray(root, "categoryCampaigns");
            var clearanceCount = CountArray(root, "clearanceOffers");
            var expiringCount = CountExpiringOffers(root, now);
            return new VendorDashboardOffersWorkspaceSnapshot(couponsCount, campaignsCount, clearanceCount, expiringCount);
        }
        catch
        {
            return new VendorDashboardOffersWorkspaceSnapshot(0, 0, 0, 0);
        }
    }

    private static int CountArray(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array
            ? property.GetArrayLength()
            : 0;
    }

    private static int CountExpiringOffers(JsonElement root, DateTime now)
    {
        var count = 0;
        var expiryWindow = now.Date.AddDays(7);

        foreach (var propertyName in new[] { "coupons", "categoryCampaigns" })
        {
            if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in property.EnumerateArray())
            {
                if (item.TryGetProperty("endsAt", out var endsAtProperty)
                    && DateTime.TryParse(endsAtProperty.GetString(), out var endsAt)
                    && endsAt.Date <= expiryWindow)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static List<VendorFinanceTrendPointResponse> BuildFinanceTrend(
        IReadOnlyCollection<dynamic> paidOrders,
        IReadOnlyCollection<dynamic> payouts)
    {
        var months = Enumerable.Range(0, 6)
            .Select(offset => DateTime.UtcNow.Date.AddMonths(-5 + offset))
            .ToList();

        return months.Select(month =>
        {
            var sales = paidOrders
                .Where(order => order.PlacedAtUtc.Year == month.Year && order.PlacedAtUtc.Month == month.Month)
                .Sum(order => (decimal)order.TotalAmount);

            var payoutsAmount = payouts
                .Where(payout =>
                {
                    var occurredAt = payout.ProcessedAtUtc ?? payout.CreatedAtUtc;
                    return payout.Status == PayoutStatus.Paid &&
                           occurredAt.Year == month.Year &&
                           occurredAt.Month == month.Month;
                })
                .Sum(payout => (decimal)payout.Amount);

            return new VendorFinanceTrendPointResponse(month.ToString("MMM"), sales, payoutsAmount);
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
    string FinancialLifecycleModeStr,
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

// New Dashboard Overview Records
public record VendorDashboardOverview(
    DateTime GeneratedAtUtc,
    string Period,
    VendorDashboardSummary Summary,
    VendorDashboardKpi Kpis,
    VendorDashboardKpiDeltas KpiDeltas,
    List<VendorDashboardTrendPoint> SalesTrend,
    List<VendorDashboardStatusSlice> OrderStatusBreakdown,
    List<VendorDashboardTopProduct> TopProducts,
    List<VendorDashboardUrgentOrder> UrgentOrders,
    List<VendorDashboardInventoryItem> InventoryWatchlist,
    VendorDashboardFinanceSnapshot FinanceSnapshot,
    VendorDashboardRatingSummary RatingSummary,
    List<VendorDashboardCompletedOrder> RecentCompleted,
    List<VendorDashboardAlert> Alerts);

public record VendorDashboardRatingStar(int Star, int Count);
public record VendorDashboardRatingSummary(decimal AverageRating, int TotalReviews, List<VendorDashboardRatingStar> Distribution);
public record VendorDashboardCompletedOrder(string Id, string OrderNumber, decimal TotalAmount, DateTime DeliveredAtUtc);

public record VendorDashboardSummary(
    int PendingOrders,
    int ReadyForPickup,
    int LateOrders,
    int LowStockProducts,
    int OpenDisputes,
    int UnrepliedLowReviews);

public record VendorDashboardKpi(
    decimal PaidSales,
    int OrdersCount,
    decimal AverageOrderValue,
    int ActiveProducts,
    int ActiveOffers,
    decimal CancellationRate,
    decimal AcceptanceRate);

public record VendorDashboardKpiDeltas(
    decimal SalesDelta,
    decimal OrdersDelta,
    decimal AovDelta);

public record VendorDashboardTrendPoint(string Date, decimal Sales, int Orders);

public record VendorDashboardStatusSlice(string Status, int Count);

public record VendorDashboardTopProduct(
    string Id,
    string NameAr,
    string NameEn,
    decimal Price,
    int SalesCount,
    int StockQuantity);

public record VendorDashboardUrgentOrder(
    string Id,
    string OrderNumber,
    string Status,
    DateTime PlacedAtUtc,
    string Reason);

public record VendorDashboardInventoryItem(
    string Id,
    string NameAr,
    string NameEn,
    int StockQuantity,
    string State);

public record VendorDashboardFinanceSnapshot(
    decimal AvailableBalance,
    decimal PendingSettlement,
    DateTime NextPayoutDate);

public record VendorDashboardAlert(
    string Severity,
    string TitleKey,
    string BodyKey,
    string Route,
    object RouteQuery);

public record VendorDashboardOverviewResponse(
    DateTime GeneratedAtUtc,
    string Period,
    VendorDashboardHeroStatsResponse HeroStats,
    VendorDashboardOrdersSectionResponse OrdersSection,
    VendorDashboardSalesSectionEnvelopeResponse SalesSection,
    VendorDashboardInventorySectionResponse InventorySection,
    VendorDashboardOffersSectionResponse OffersSection,
    VendorDashboardReviewsSectionResponse ReviewsSection,
    VendorDashboardFinanceSectionResponse FinanceSection,
    VendorDashboardDisputesSectionResponse DisputesSection,
    VendorDashboardStaffSectionResponse StaffSection,
    List<VendorDashboardAlertItemResponse> AlertsFeed);

public record VendorDashboardHeroStatsResponse(
    int PendingOrders,
    int LateOrders,
    int ReadyForPickup,
    int DriverIssues,
    int OpenDisputes,
    int LowStockCritical);

public record VendorDashboardOrdersSectionResponse(
    int PendingOrders,
    int LateOrders,
    int ReadyForPickup,
    int DriverIssues,
    int OpenDisputes,
    int LowStockCritical,
    decimal PrepEfficiencyScore,
    int AveragePrepTimeMinutes,
    List<VendorDashboardDualTrendPointResponse> OrdersTrend,
    List<VendorDashboardBreakdownSliceResponse> StatusBreakdown,
    List<VendorDashboardBreakdownSliceResponse> Funnel,
    List<VendorDashboardUrgentOrderResponse> UrgentOrders,
    List<VendorDashboardAlertItemResponse> LatestAlerts);

public record VendorDashboardSalesSectionEnvelopeResponse(
    VendorDashboardSalesSectionResponse Data,
    VendorDashboardDeltaSummaryResponse Deltas);

public record VendorDashboardSalesSectionResponse(
    decimal GrossSales,
    decimal PaidSales,
    int OrdersCount,
    decimal AverageOrderValue,
    decimal CancellationRate,
    decimal RefundRate,
    decimal LostRevenueAmount,
    List<VendorDashboardDualTrendPointResponse> SalesVsOrdersTrend,
    List<VendorDashboardBreakdownSliceResponse> WeekdayPerformance,
    List<VendorDashboardRankedItemResponse> TopProducts,
    List<VendorDashboardBreakdownSliceResponse> TopCategories,
    List<VendorDashboardRankedItemResponse> UnderperformingProducts);

public record VendorDashboardInventorySectionResponse(
    int ActiveProducts,
    int OutOfStock,
    int LowStock,
    int InactiveProducts,
    int ProductsWithOffers,
    decimal IdleCapitalAmount,
    List<VendorDashboardBreakdownSliceResponse> StockHealthDistribution,
    List<VendorDashboardRankedItemResponse> InventoryRiskList,
    List<VendorDashboardTrendPointResponse> CatalogGrowth,
    List<VendorDashboardRankedItemResponse> CriticalStockWatchlist,
    List<VendorDashboardRankedItemResponse> NoMovementProducts);

public record VendorDashboardOffersSectionResponse(
    int ActiveOffers,
    int ClearanceItems,
    int ExpiringOffers,
    decimal OfferCoverage,
    List<VendorDashboardBreakdownSliceResponse> OffersByType,
    List<VendorDashboardBreakdownSliceResponse> DiscountBands,
    List<VendorDashboardBreakdownSliceResponse> LinkedProductsByType,
    List<VendorDashboardRankedItemResponse> ExpiringOffersList,
    List<VendorDashboardRankedItemResponse> PromotionCandidates);

public record VendorDashboardReviewsSectionResponse(
    decimal AverageRating,
    int TotalReviews,
    int LowRatingCount,
    int PendingReplies,
    int HiddenReviews,
    List<VendorDashboardBreakdownSliceResponse> RatingDistribution,
    List<VendorDashboardTrendPointResponse> ReviewsTrend,
    List<VendorDashboardBreakdownSliceResponse> ReplyBreakdown,
    List<VendorDashboardReviewListItemResponse> LowRatingUnreplied,
    List<VendorDashboardReviewListItemResponse> RecentHighlights);

public record VendorDashboardFinanceSectionResponse(
    decimal AvailableBalance,
    decimal PendingSettlement,
    decimal NetSales,
    decimal Fees,
    decimal PayoutsPaid,
    decimal HoldAmount,
    DateTime? NextSettlementAt,
    string FinancialLifecycleMode,
    List<VendorDashboardDualTrendPointResponse> SalesVsPayoutsTrend,
    List<VendorDashboardBreakdownSliceResponse> SettlementStatusBreakdown,
    List<VendorDashboardBreakdownSliceResponse> LedgerTypeBreakdown,
    List<VendorDashboardSettlementListItemResponse> RecentSettlements,
    List<VendorDashboardLedgerListItemResponse> RecentLedgerEntries);

public record VendorDashboardDisputesSectionResponse(
    int OpenDisputes,
    int HighPriorityDisputes,
    int RefundRequests,
    int AwaitingVendorResponse,
    List<VendorDashboardBreakdownSliceResponse> StatusBreakdown,
    List<VendorDashboardBreakdownSliceResponse> TypeBreakdown,
    List<VendorDashboardTrendPointResponse> DisputeTrend,
    List<VendorDashboardDisputeListItemResponse> AwaitingAction,
    List<VendorDashboardDisputeListItemResponse> RecentEscalations);

public record VendorDashboardStaffSectionResponse(
    int ActiveBranches,
    int ActiveStaff,
    int PendingInvitations,
    int BranchesNeedingCoverage,
    List<VendorDashboardBreakdownSliceResponse> BranchStatusBreakdown,
    List<VendorDashboardBreakdownSliceResponse> StaffRoleDistribution);

public record VendorDashboardAlertItemResponse(
    string Id,
    string Domain,
    string Severity,
    string TitleKey,
    string BodyKey,
    string Route,
    Dictionary<string, string> RouteQuery);

public record VendorDashboardDeltaSummaryResponse(decimal SalesDelta, decimal OrdersDelta, decimal AverageOrderValueDelta);
public record VendorDashboardTrendPointResponse(string Label, int Value);
public record VendorDashboardDualTrendPointResponse(string Label, decimal Value, decimal SecondaryValue);
public record VendorDashboardBreakdownSliceResponse(string Key, string Label, int Value);
public record VendorDashboardRankedItemResponse(string Id, string LabelAr, string LabelEn, decimal Metric, decimal SecondaryMetric);
public record VendorDashboardUrgentOrderResponse(string Id, string OrderNumber, string Status, DateTime PlacedAtUtc, string ReasonKey);
public record VendorDashboardReviewListItemResponse(string Id, string CustomerName, int Rating, string Comment, DateTime CreatedAtUtc, bool NeedsAttention);
public record VendorDashboardSettlementListItemResponse(string Id, string Code, decimal Amount, string Status, DateTime OccurredAtUtc, int OrdersCount);
public record VendorDashboardLedgerListItemResponse(string Id, string Type, string Label, decimal Amount, string Direction, DateTime OccurredAtUtc, string Reference);
public record VendorDashboardDisputeListItemResponse(string Id, string Type, string Status, string Priority, string Message, DateTime OccurredAtUtc);
public record VendorDashboardTrendBucket(string Label, int Value, decimal SecondaryValue);
public record VendorDashboardOffersWorkspaceSnapshot(int CouponsCount, int CategoryCampaignsCount, int ClearanceOffersCount, int ExpiringOffersCount);
