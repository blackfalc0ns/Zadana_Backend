using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Dashboard;
using Zadana.Application.Modules.Dashboard.DTOs;
using Zadana.Application.Modules.Geography;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Application.Modules.Dashboard.Queries.GetAdminDashboardOverview;

internal sealed class GetAdminDashboardOverviewQueryHandler(
    IApplicationDbContext dbContext,
    IAppCache cache,
    IOptions<CachingSettings> cachingOptions,
    IGeographyCityResolver geographyCityResolver)
    : IRequestHandler<GetAdminDashboardOverviewQuery, AdminDashboardOverviewDto>
{
    private readonly CacheDurationSettings _durations = cachingOptions.Value.Durations;

    public Task<AdminDashboardOverviewDto> Handle(GetAdminDashboardOverviewQuery request, CancellationToken cancellationToken)
    {
        var period = NormalizePeriod(request.Period);
        var normalizedRegion = DashboardGeographyScope.NormalizeFilterRegionToken(request.Region);

        return cache.GetOrCreateAsync(
            AppCacheKeys.Build(
                "dashboard",
                "admin-overview",
                "v3-saudi-regions",
                period,
                AppCacheKeys.NormalizeToken(normalizedRegion),
                AppCacheKeys.GuidToken(request.VendorId),
                AppCacheKeys.CurrentCulture),
            async token =>
            {
                var now = DateTime.UtcNow;
                var start = ResolveStart(period, now);

                await geographyCityResolver.RefreshCatalogAsync(token);
                var saudiRegions = await dbContext.SaudiRegions
                    .AsNoTracking()
                    .OrderBy(region => region.SortOrder)
                    .Select(region => new SaudiRegionRow(
                        region.Code,
                        region.NameAr,
                        region.NameEn,
                        region.SortOrder))
                    .ToListAsync(token);

                var geography = new DashboardGeographyScope(geographyCityResolver, saudiRegions);

                var vendors = await dbContext.Vendors
            .Select(v => new VendorRow(
                v.Id,
                v.BusinessNameAr,
                v.BusinessNameEn,
                v.Region,
                v.City,
                v.Status,
                v.AcceptOrders,
                v.LockedAtUtc,
                v.UpdatedAtUtc,
                v.CreatedAtUtc))
            .ToListAsync(token);

                var vendorIndex = vendors.ToDictionary(v => v.Id);
                var selectedVendor = request.VendorId.HasValue && vendorIndex.TryGetValue(request.VendorId.Value, out var vendor)
                    ? vendor
                    : null;

                var orders = await dbContext.Orders
            .Where(o => o.PlacedAtUtc >= start)
            .Select(o => new OrderRow(
                o.Id,
                o.OrderNumber,
                o.VendorId,
                o.Status,
                o.PaymentStatus,
                o.TotalAmount,
                o.CommissionAmount,
                o.DeliveryFee,
                o.DeliveredAtUtc,
                o.CancelledAtUtc,
                o.PlacedAtUtc))
            .ToListAsync(cancellationToken);

                var drivers = await dbContext.Drivers
            .Select(d => new DriverRow(
                d.Id,
                d.UserId,
                d.Region,
                d.City,
                d.Status,
                d.VerificationStatus,
                d.IsAvailable,
                d.IsLocationUpdatesBlocked,
                d.UpdatedAtUtc,
                d.CreatedAtUtc))
            .ToListAsync(cancellationToken);

                var supportCases = await dbContext.OrderSupportCases
            .Where(c => c.CreatedAtUtc >= start || (c.ClosedAtUtc.HasValue && c.ClosedAtUtc >= start))
            .Select(c => new SupportCaseRow(
                c.Id,
                c.OrderId,
                c.Status,
                c.Priority,
                c.Queue,
                c.RequestedRefundAmount,
                c.ApprovedRefundAmount,
                c.AwaitingResponseFromRole,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                c.ClosedAtUtc))
            .ToListAsync(cancellationToken);

                var productsCount = await dbContext.MasterProducts.CountAsync(cancellationToken);
                var brandsCount = await dbContext.Brands.CountAsync(cancellationToken);
                var categoriesCount = await dbContext.Categories.CountAsync(cancellationToken);
                var vendorBranchesCount = await dbContext.VendorBranches.CountAsync(cancellationToken);
                var activeCouponsCount = await dbContext.Coupons.CountAsync(c => c.IsActive, cancellationToken);
                var activeBannersCount = await dbContext.HomeBanners.CountAsync(cancellationToken);
                var featuredPlacementsCount = await dbContext.FeaturedProductPlacements.CountAsync(cancellationToken);
                var permissionDefinitionsCount = await dbContext.PermissionDefinitions.CountAsync(cancellationToken);
                var rolesCount = await dbContext.RoleDefinitions.CountAsync(cancellationToken);
                var userAccessScopesCount = await dbContext.UserAccessScopes.CountAsync(cancellationToken);
                var userPermissionOverridesCount = await dbContext.UserPermissionOverrides.CountAsync(cancellationToken);
                var walletsCount = await dbContext.Wallets.CountAsync(cancellationToken);

                var adminUsers = await dbContext.Users
                    .Where(u => u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin)
                    .Select(u => new UserRow(
                        u.Id,
                        u.FullName,
                        u.Role,
                        u.AccountStatus,
                        u.IsLoginLocked,
                        u.PermissionVersion,
                        u.CreatedAtUtc,
                        u.LastLoginAtUtc,
                        u.LastSeenAtUtc))
                    .ToListAsync(cancellationToken);

                var vendorProducts = await dbContext.VendorProducts
            .Select(vp => new VendorProductRow(
                vp.Id,
                vp.VendorId,
                vp.StockQuantity,
                vp.IsAvailable,
                vp.Status,
                vp.CreatedAtUtc))
            .ToListAsync(cancellationToken);

                var activeUsersCount = await dbContext.Users.CountAsync(
                    u => u.LastLoginAtUtc.HasValue && u.LastLoginAtUtc.Value >= start,
                    cancellationToken);
                var adminUsersCount = adminUsers.Count;
                var lockedAdminUsersCount = adminUsers.Count(u => u.IsLoginLocked);
                var customersTotal = await dbContext.Users.CountAsync(u => u.Role == UserRole.Customer, cancellationToken);
                var newCustomers = await dbContext.Users.CountAsync(
                    u => u.Role == UserRole.Customer && u.CreatedAtUtc >= start,
                    cancellationToken);
                var activeCustomers = await dbContext.Users.CountAsync(
                    u => u.Role == UserRole.Customer && u.LastLoginAtUtc.HasValue && u.LastLoginAtUtc.Value >= start,
                    cancellationToken);
                var unreadNotifications = await dbContext.Notifications.CountAsync(n => !n.IsRead, cancellationToken);
                var recentNotifications = await dbContext.Notifications.CountAsync(n => n.CreatedAtUtc >= start, cancellationToken);
                var pushDevicesCount = await dbContext.UserPushDevices.CountAsync(cancellationToken);
                var pendingDocumentReviews = await dbContext.VendorDocumentReviews.CountAsync(d => d.Decision == VendorDocumentReviewDecision.Pending, cancellationToken);
                var pendingProductRequests = await dbContext.ProductRequests.CountAsync(p => p.Status == ApprovalStatus.Pending, cancellationToken);
                var pendingBrandRequests = await dbContext.BrandRequests.CountAsync(p => p.Status == ApprovalStatus.Pending, cancellationToken);
                var pendingCategoryRequests = await dbContext.CategoryRequests.CountAsync(p => p.Status == ApprovalStatus.Pending, cancellationToken);
                var pendingSettlements = await dbContext.Settlements.CountAsync(
                    s => s.Status == SettlementStatus.Pending || s.Status == SettlementStatus.PendingReview,
                    cancellationToken);
                var failedSettlements = await dbContext.Settlements.CountAsync(s => s.Status == SettlementStatus.Failed, cancellationToken);
                var settledNetAmount = await dbContext.Settlements
            .Where(s => s.ProcessedAtUtc.HasValue && s.ProcessedAtUtc.Value >= start && s.Status == SettlementStatus.Settled)
            .SumAsync(s => (decimal?)s.NetAmount, cancellationToken) ?? 0m;
                var walletInflow = await dbContext.WalletTransactions
            .Where(t => t.CreatedAtUtc >= start && t.Direction == "IN")
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;
                var walletOutflow = await dbContext.WalletTransactions
            .Where(t => t.CreatedAtUtc >= start && t.Direction == "OUT")
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;
                var pendingWithdrawals = await dbContext.DriverWithdrawalRequests.CountAsync(w => w.Status == DriverWithdrawalStatus.Pending, cancellationToken);
                var processingWithdrawals = await dbContext.DriverWithdrawalRequests.CountAsync(w => w.Status == DriverWithdrawalStatus.Processing, cancellationToken);
                var refundsTotal = await dbContext.Refunds
            .Where(r => r.CreatedAtUtc >= start)
            .SumAsync(r => (decimal?)r.Amount, cancellationToken) ?? 0m;
                var refundsCount = await dbContext.Refunds.CountAsync(r => r.CreatedAtUtc >= start, cancellationToken);
                var paymentsFailedCount = await dbContext.Payments.CountAsync(p => p.CreatedAtUtc >= start && p.Status == PaymentStatus.Failed, cancellationToken);
                var paymentsPendingCount = await dbContext.Payments.CountAsync(p => p.CreatedAtUtc >= start && p.Status == PaymentStatus.Pending, cancellationToken);
                var openDriverIncidents = await dbContext.DriverIncidents.CountAsync(i => i.Status != DriverIncidentStatus.Resolved, cancellationToken);
                var lowStockProducts = vendorProducts.Count(vp => vp.StockQuantity <= 5);
                var unavailableProducts = vendorProducts.Count(vp => !vp.IsAvailable || vp.Status is VendorProductStatus.Inactive or VendorProductStatus.OutOfStock or VendorProductStatus.Suspended);

        var filteredOrders = orders
            .Where(order => MatchesVendor(order, request.VendorId) && MatchesRegion(order, vendorIndex, geography, normalizedRegion))
            .ToList();

        var filteredSupportCases = supportCases
            .Where(supportCase =>
            {
                var linkedOrder = supportCase.OrderId.HasValue
                    ? filteredOrders.FirstOrDefault(order => order.Id == supportCase.OrderId.Value)
                    : null;
                return linkedOrder is not null;
            })
            .ToList();

        var filteredVendors = vendors
            .Where(v => MatchesVendor(v, request.VendorId) && MatchesRegion(v, geography, normalizedRegion))
            .ToList();

        var filteredDrivers = drivers
            .Where(d => MatchesRegion(d, geography, normalizedRegion) && MatchesDriverVendorScope(d, filteredOrders, request.VendorId))
            .ToList();

        var filteredVendorProducts = vendorProducts
            .Where(vp => MatchesVendor(vp, request.VendorId) && MatchesRegion(vp, vendorIndex, geography, normalizedRegion))
            .ToList();

        var totalOrders = filteredOrders.Count;
        var completedOrders = filteredOrders.Count(o => o.Status == OrderStatus.Delivered);
        var riskOrders = filteredOrders.Count(IsOrderAtRisk);
        var paymentIssues = filteredOrders.Count(o => o.PaymentStatus is PaymentStatus.Failed or PaymentStatus.Pending or PaymentStatus.PendingCollection);
        var gmv = filteredOrders.Sum(o => o.TotalAmount);
        var refundExposure = filteredOrders
            .Where(o => o.Status is OrderStatus.Cancelled or OrderStatus.Refunded || o.PaymentStatus is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
            .Sum(o => o.TotalAmount);
        var lateOrders = filteredOrders.Count(o =>
            o.Status is OrderStatus.Preparing or OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned or OrderStatus.PickedUp or OrderStatus.OnTheWay
            && o.PlacedAtUtc <= now.AddMinutes(-45));
        var onTimeRate = totalOrders == 0
            ? 100m
            : Math.Round((decimal)Math.Max(0, completedOrders - lateOrders) / Math.Max(1, completedOrders) * 100m, 1);

        var vendorBacklog = filteredVendors.Count(v => v.Status != VendorStatus.Active || !v.AcceptOrders || v.LockedAtUtc.HasValue);
        var driverBacklog = filteredDrivers.Count(d => ResolveDriverReadiness(d) != "ready");

        var ordersTrend = BuildOrdersTrend(filteredOrders, period, start, now);
        var revenueTrend = BuildRevenueTrend(filteredOrders, period, start, now);
        var regionPressure = BuildRegionPressure(filteredOrders, filteredDrivers, vendorIndex, geography, normalizedRegion);
        var vendorReadiness = BuildVendorReadiness(filteredVendors);
        var driverReadiness = BuildDriverReadiness(filteredDrivers);
        var liveQueues = BuildLiveQueues(filteredOrders);
        var riskQueues = BuildRiskQueues(filteredSupportCases, vendorBacklog, driverBacklog);
        var alerts = BuildAlerts(paymentIssues, filteredSupportCases, vendorBacklog, driverBacklog, regionPressure);
        var attentionItems = BuildAttentionItems(filteredOrders, filteredSupportCases, filteredVendors, filteredDrivers, vendorIndex);
        var auditFeed = BuildAuditFeed(filteredOrders, filteredSupportCases, filteredVendors, filteredDrivers);
        var sections = new AdminDashboardSectionBundleDto
        {
            SystemHealth = BuildSystemHealthSection(adminUsersCount, activeUsersCount, unreadNotifications, pushDevicesCount, alerts.Count),
            OrderOps = BuildOrderOpsSection(filteredOrders, filteredSupportCases, vendorIndex, gmv, lateOrders, paymentIssues),
            VendorOps = BuildVendorOpsSection(filteredVendors, filteredOrders, filteredSupportCases, pendingDocumentReviews, vendorBranchesCount, vendorIndex, geography),
            DriverOps = BuildDriverOpsSection(filteredDrivers, regionPressure, openDriverIncidents, pendingWithdrawals, processingWithdrawals),
            CustomerSupport = BuildCustomerSupportSection(customersTotal, newCustomers, activeCustomers, filteredSupportCases),
            FinanceOps = BuildFinanceOpsSection(gmv, refundsTotal, refundsCount, paymentsFailedCount, paymentsPendingCount, pendingSettlements, failedSettlements, settledNetAmount, walletsCount, walletInflow, walletOutflow),
            CatalogHealth = BuildCatalogHealthSection(productsCount, brandsCount, categoriesCount, filteredVendorProducts, pendingProductRequests, pendingBrandRequests, pendingCategoryRequests, lowStockProducts, unavailableProducts, vendorIndex),
            MarketingPulse = BuildMarketingPulseSection(activeCouponsCount, activeBannersCount, featuredPlacementsCount, unreadNotifications, recentNotifications),
            AccessSecurity = BuildAccessSecuritySection(rolesCount, permissionDefinitionsCount, userAccessScopesCount, userPermissionOverridesCount, adminUsersCount, lockedAdminUsersCount, adminUsers)
        };

        return new AdminDashboardOverviewDto
        {
            Meta = new AdminDashboardMetaDto
            {
                Period = period,
                Region = normalizedRegion,
                VendorId = request.VendorId,
                ScopeSummary = ResolveScopeSummary(geography, normalizedRegion, selectedVendor),
                Mode = "live",
                GeneratedAtUtc = now
            },
            Filters = new AdminDashboardFilterOptionsDto
            {
                DateRanges =
                [
                    new AdminDashboardFilterOptionDto { Value = "today", Label = "اليوم" },
                    new AdminDashboardFilterOptionDto { Value = "week", Label = "آخر 7 أيام" },
                    new AdminDashboardFilterOptionDto { Value = "month", Label = "آخر 30 يوم" }
                ],
                Regions = BuildRegionOptions(vendors, geography),
                Vendors = BuildVendorOptions(vendors, geography, normalizedRegion)
            },
            HeroKpis =
            [
                new AdminDashboardKpiDto
                {
                    Id = "gmv",
                    LabelKey = "DASHBOARD.KPI.GMV",
                    Value = gmv,
                    DisplayValue = Math.Round(gmv, 0).ToString("N0"),
                    Unit = "ر.س",
                    ChangeLabel = FormatChange(totalOrders, completedOrders, "up"),
                    TrendDirection = "up",
                    Severity = "neutral",
                    ContextKey = "DASHBOARD.KPI_CONTEXT.GMV"
                },
                new AdminDashboardKpiDto
                {
                    Id = "completed-orders",
                    LabelKey = "DASHBOARD.KPI.COMPLETED_ORDERS",
                    Value = completedOrders,
                    DisplayValue = completedOrders.ToString("N0"),
                    ChangeLabel = FormatChange(completedOrders, totalOrders, "up"),
                    TrendDirection = "up",
                    Severity = "success",
                    ContextKey = "DASHBOARD.KPI_CONTEXT.COMPLETED_ORDERS"
                },
                new AdminDashboardKpiDto
                {
                    Id = "on-time-rate",
                    LabelKey = "DASHBOARD.KPI.ON_TIME_RATE",
                    Value = onTimeRate,
                    DisplayValue = onTimeRate.ToString("N1"),
                    Unit = "%",
                    ChangeLabel = onTimeRate >= 90m ? "+ مستقر" : "- يحتاج إجراء",
                    TrendDirection = onTimeRate >= 90m ? "up" : "down",
                    Severity = onTimeRate >= 90m ? "success" : "warning",
                    ContextKey = "DASHBOARD.KPI_CONTEXT.ON_TIME_RATE"
                },
                new AdminDashboardKpiDto
                {
                    Id = "orders-at-risk",
                    LabelKey = "DASHBOARD.KPI.ORDERS_AT_RISK",
                    Value = riskOrders,
                    DisplayValue = riskOrders.ToString("N0"),
                    ChangeLabel = $"+{paymentIssues} دفع",
                    TrendDirection = riskOrders > 0 ? "up" : "flat",
                    Severity = riskOrders > 0 ? "critical" : "success",
                    ContextKey = "DASHBOARD.KPI_CONTEXT.ORDERS_AT_RISK"
                },
                new AdminDashboardKpiDto
                {
                    Id = "open-dispute-exposure",
                    LabelKey = "DASHBOARD.KPI.DISPUTE_EXPOSURE",
                    Value = refundExposure,
                    DisplayValue = Math.Round(refundExposure, 0).ToString("N0"),
                    Unit = "ر.س",
                    ChangeLabel = filteredSupportCases.Count(c => c.Status is not (OrderSupportCaseStatus.Resolved or OrderSupportCaseStatus.Rejected)).ToString("N0"),
                    TrendDirection = refundExposure > 0 ? "up" : "flat",
                    Severity = refundExposure > 0 ? "warning" : "success",
                    ContextKey = "DASHBOARD.KPI_CONTEXT.DISPUTE_EXPOSURE"
                },
                new AdminDashboardKpiDto
                {
                    Id = "supply-backlog",
                    LabelKey = "DASHBOARD.KPI.SUPPLY_BACKLOG",
                    Value = vendorBacklog + driverBacklog,
                    DisplayValue = $"{vendorBacklog:N0} + {driverBacklog:N0}",
                    ChangeLabel = "التجار + السائقين",
                    TrendDirection = vendorBacklog + driverBacklog > 0 ? "up" : "flat",
                    Severity = vendorBacklog + driverBacklog > 0 ? "warning" : "success",
                    ContextKey = "DASHBOARD.KPI_CONTEXT.SUPPLY_BACKLOG"
                }
            ],
            Charts = new AdminDashboardChartBundleDto
            {
                OrdersTrend = ordersTrend,
                RevenueTrend = revenueTrend,
                RegionPressure = regionPressure,
                VendorReadiness = vendorReadiness,
                DriverReadiness = driverReadiness
            },
            Alerts = alerts,
            Queues = new AdminDashboardQueueBundleDto
            {
                Live = liveQueues,
                Risk = riskQueues
            },
            AttentionItems = attentionItems,
            AuditFeed = auditFeed,
            Sections = sections
        };
            },
            new AppCacheEntryOptions(_durations.AdminDashboard),
            [CacheTagNames.Dashboard],
            cancellationToken);
    }

    private static string NormalizePeriod(string? period) =>
        period?.Trim().ToLowerInvariant() switch
        {
            "week" => "week",
            "month" => "month",
            _ => "today"
        };

    private static DateTime ResolveStart(string period, DateTime now) =>
        period switch
        {
            "week" => now.Date.AddDays(-6),
            "month" => now.Date.AddDays(-29),
            _ => now.Date
        };

    private static bool MatchesVendor(OrderRow order, Guid? vendorId) =>
        !vendorId.HasValue || order.VendorId == vendorId.Value;

    private static bool MatchesVendor(VendorRow vendor, Guid? vendorId) =>
        !vendorId.HasValue || vendor.Id == vendorId.Value;

    private static bool MatchesVendor(VendorProductRow product, Guid? vendorId) =>
        !vendorId.HasValue || product.VendorId == vendorId.Value;

    private static bool MatchesRegion(
        OrderRow order,
        IReadOnlyDictionary<Guid, VendorRow> vendorIndex,
        DashboardGeographyScope geography,
        string normalizedFilterRegion)
    {
        if (!vendorIndex.TryGetValue(order.VendorId, out var vendor))
        {
            return false;
        }

        return geography.MatchesRegion(
            geography.ResolveEntityRegionCode(vendor.City, vendor.Region),
            normalizedFilterRegion);
    }

    private static bool MatchesRegion(
        VendorRow vendor,
        DashboardGeographyScope geography,
        string normalizedFilterRegion) =>
        geography.MatchesRegion(
            geography.ResolveEntityRegionCode(vendor.City, vendor.Region),
            normalizedFilterRegion);

    private static bool MatchesRegion(
        VendorProductRow product,
        IReadOnlyDictionary<Guid, VendorRow> vendorIndex,
        DashboardGeographyScope geography,
        string normalizedFilterRegion)
    {
        if (!vendorIndex.TryGetValue(product.VendorId, out var vendor))
        {
            return false;
        }

        return geography.MatchesRegion(
            geography.ResolveEntityRegionCode(vendor.City, vendor.Region),
            normalizedFilterRegion);
    }

    private static bool MatchesRegion(
        DriverRow driver,
        DashboardGeographyScope geography,
        string normalizedFilterRegion) =>
        geography.MatchesRegion(
            geography.ResolveEntityRegionCode(driver.City, driver.Region),
            normalizedFilterRegion);

    private static bool MatchesDriverVendorScope(DriverRow driver, IReadOnlyCollection<OrderRow> filteredOrders, Guid? vendorId)
    {
        if (!vendorId.HasValue)
        {
            return true;
        }

        var activeRegions = filteredOrders.Select(o => o.VendorId).Distinct().ToHashSet();
        return activeRegions.Count > 0;
    }

    private static string ResolveScopeSummary(
        DashboardGeographyScope geography,
        string normalizedFilterRegion,
        VendorRow? vendor)
    {
        if (vendor is not null)
        {
            return !string.IsNullOrWhiteSpace(vendor.BusinessNameAr) ? vendor.BusinessNameAr : vendor.BusinessNameEn;
        }

        return normalizedFilterRegion == GeographyCoverageConstants.AllRegionsToken
            ? "كل الشبكة"
            : geography.GetRegionLabel(normalizedFilterRegion);
    }

    private static string TranslateDashboardToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim() switch
        {
            "All Network" => "كل الشبكة",
            "Central Region" => "المنطقة الوسطى",
            "Western Region" => "المنطقة الغربية",
            "Eastern Region" => "المنطقة الشرقية",
            "Northern Region" => "المنطقة الشمالية",
            "Southern Region" => "المنطقة الجنوبية",
            "Other Regions" => "مناطق أخرى",
            "Vendor" => "تاجر",
            "Vendors" => "التجار",
            "Driver" => "سائق",
            "Drivers" => "السائقين",
            "DriverOps" => "عمليات السائقين",
            "VendorOps" => "عمليات التجار",
            "Finance" => "المالية",
            "FinanceOps" => "المالية",
            "Operations" => "العمليات",
            "Support" => "الدعم",
            "CustomerSupport" => "دعم العملاء",
            "Customer Experience" => "تجربة العملاء",
            "Risk" => "المخاطر",
            "Legal" => "الشؤون القانونية",
            "Catalog" => "الكتالوج",
            "Wallets" => "المحافظ",
            "Submitted" => "مقدمة",
            "InReview" => "قيد المراجعة",
            "AwaitingCustomerEvidence" => "في انتظار إثبات العميل",
            "Escalated" => "مصعدة",
            "Resolved" => "محلولة",
            "Rejected" => "مرفوضة",
            "Pending" => "معلقة",
            "Processing" => "قيد المعالجة",
            "Active" => "نشط",
            "Inactive" => "غير نشط",
            "Suspended" => "موقوف",
            "Blocked" => "محظور",
            "Banned" => "محظور",
            "PendingReview" => "قيد المراجعة",
            "SuperAdmin" => "مدير عام",
            "Admin" => "مشرف",
            _ => value
        };
    }

    private static IReadOnlyList<AdminDashboardFilterOptionDto> BuildRegionOptions(
        IEnumerable<VendorRow> vendors,
        DashboardGeographyScope geography)
    {
        var vendorList = vendors.ToList();
        var counts = vendorList
            .GroupBy(vendor => geography.ResolveEntityRegionCode(vendor.City, vendor.Region))
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var options = new List<AdminDashboardFilterOptionDto>
        {
            new()
            {
                Value = GeographyCoverageConstants.AllRegionsToken,
                Label = geography.GetRegionLabel(GeographyCoverageConstants.AllRegionsToken),
                Count = vendorList.Count
            }
        };

        foreach (var region in geography.Regions)
        {
            options.Add(new AdminDashboardFilterOptionDto
            {
                Value = region.Code,
                Label = region.NameAr,
                Count = counts.GetValueOrDefault(region.Code)
            });
        }

        if (counts.TryGetValue(DashboardGeographyScope.UnmappedRegionCode, out var unmappedCount) && unmappedCount > 0)
        {
            options.Add(new AdminDashboardFilterOptionDto
            {
                Value = DashboardGeographyScope.UnmappedRegionCode,
                Label = geography.GetRegionLabel(DashboardGeographyScope.UnmappedRegionCode),
                Count = unmappedCount
            });
        }

        return options;
    }

    private static IReadOnlyList<AdminDashboardFilterOptionDto> BuildVendorOptions(
        IEnumerable<VendorRow> vendors,
        DashboardGeographyScope geography,
        string normalizedFilterRegion)
    {
        var scopedVendors = vendors
            .Where(vendor => geography.MatchesRegion(
                geography.ResolveEntityRegionCode(vendor.City, vendor.Region),
                normalizedFilterRegion))
            .OrderBy(vendor => vendor.BusinessNameEn)
            .ToList();

        var items = scopedVendors
            .Select(vendor => new AdminDashboardFilterOptionDto
            {
                Value = vendor.Id.ToString(),
                Label = !string.IsNullOrWhiteSpace(vendor.BusinessNameAr)
                    ? vendor.BusinessNameAr
                    : vendor.BusinessNameEn
            })
            .ToList();

        items.Insert(0, new AdminDashboardFilterOptionDto
        {
            Value = "all",
            Label = "كل التجار",
            Count = scopedVendors.Count
        });

        return items;
    }

    private static AdminDashboardSeriesChartDto BuildOrdersTrend(IReadOnlyList<OrderRow> orders, string period, DateTime start, DateTime now)
    {
        var buckets = BuildTimeBuckets(period, start, now);
        var totalSeries = buckets.Select(bucket => new AdminDashboardChartPointDto
        {
            Label = bucket.Label,
            Value = orders.Count(order => bucket.Contains(order.PlacedAtUtc))
        }).ToList();
        var deliveredSeries = buckets.Select(bucket => new AdminDashboardChartPointDto
        {
            Label = bucket.Label,
            Value = orders.Count(order => order.DeliveredAtUtc.HasValue && bucket.Contains(order.DeliveredAtUtc.Value))
        }).ToList();

        return new AdminDashboardSeriesChartDto
        {
            TitleKey = "DASHBOARD.CHARTS.ORDER_FUNNEL",
            DescriptionKey = "DASHBOARD.CHARTS.ORDER_FUNNEL_DESC",
            Series =
            [
                new AdminDashboardChartSeriesDto
                {
                    Id = "total_orders",
                    LabelKey = "DASHBOARD.CHARTS.SERIES_TOTAL_ORDERS",
                    Color = "#127C8C",
                    Points = totalSeries
                },
                new AdminDashboardChartSeriesDto
                {
                    Id = "delivered_orders",
                    LabelKey = "DASHBOARD.CHARTS.SERIES_DELIVERED_ORDERS",
                    Color = "#1FA3B5",
                    Points = deliveredSeries
                }
            ]
        };
    }

    private static AdminDashboardSeriesChartDto BuildRevenueTrend(IReadOnlyList<OrderRow> orders, string period, DateTime start, DateTime now)
    {
        var buckets = BuildTimeBuckets(period, start, now);
        var revenueSeries = buckets.Select(bucket => new AdminDashboardChartPointDto
        {
            Label = bucket.Label,
            Value = orders.Where(order => bucket.Contains(order.PlacedAtUtc)).Sum(order => order.TotalAmount)
        }).ToList();
        var refundsSeries = buckets.Select(bucket => new AdminDashboardChartPointDto
        {
            Label = bucket.Label,
            Value = orders.Where(order => bucket.Contains(order.PlacedAtUtc) && (order.Status is OrderStatus.Cancelled or OrderStatus.Refunded || order.PaymentStatus is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded))
                .Sum(order => order.TotalAmount)
        }).ToList();

        return new AdminDashboardSeriesChartDto
        {
            TitleKey = "DASHBOARD.CHARTS.REVENUE_QUALITY",
            DescriptionKey = "DASHBOARD.CHARTS.REVENUE_QUALITY_DESC",
            Series =
            [
                new AdminDashboardChartSeriesDto
                {
                    Id = "gmv",
                    LabelKey = "DASHBOARD.CHARTS.GMV_LINE",
                    Color = "#127C8C",
                    Points = revenueSeries
                },
                new AdminDashboardChartSeriesDto
                {
                    Id = "refunds",
                    LabelKey = "DASHBOARD.CHARTS.REFUND_LINE",
                    Color = "#EF4444",
                    Points = refundsSeries
                }
            ]
        };
    }

    private static IReadOnlyList<AdminDashboardRegionPressureDto> BuildRegionPressure(
        IReadOnlyList<OrderRow> orders,
        IReadOnlyList<DriverRow> drivers,
        IReadOnlyDictionary<Guid, VendorRow> vendorIndex,
        DashboardGeographyScope geography,
        string selectedRegion)
    {
        var regionGroups = orders
            .GroupBy(order => vendorIndex.TryGetValue(order.VendorId, out var vendor)
                ? geography.ResolveEntityRegionCode(vendor.City, vendor.Region)
                : DashboardGeographyScope.UnmappedRegionCode)
            .ToDictionary(group => group.Key, group => new RegionAccumulator
            {
                RegionKey = group.Key,
                RegionLabel = geography.GetRegionLabel(group.Key),
                LateOrders = group.Count(order => order.Status is OrderStatus.Preparing or OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned or OrderStatus.PickedUp or OrderStatus.OnTheWay),
                PaymentIssues = group.Count(order => order.PaymentStatus is PaymentStatus.Failed or PaymentStatus.Pending or PaymentStatus.PendingCollection)
            });

        foreach (var driver in drivers)
        {
            var regionKey = geography.ResolveEntityRegionCode(driver.City, driver.Region);
            if (!regionGroups.TryGetValue(regionKey, out var accumulator))
            {
                accumulator = new RegionAccumulator
                {
                    RegionKey = regionKey,
                    RegionLabel = geography.GetRegionLabel(regionKey)
                };
                regionGroups[regionKey] = accumulator;
            }

            if (ResolveDriverReadiness(driver) != "ready")
            {
                accumulator.DriverGap += 1;
            }
        }

        return regionGroups.Values
            .Where(row => selectedRegion == GeographyCoverageConstants.AllRegionsToken || row.RegionKey == selectedRegion)
            .Select(row => new AdminDashboardRegionPressureDto
            {
                RegionKey = row.RegionKey,
                RegionLabel = row.RegionLabel,
                LateOrders = row.LateOrders,
                PaymentIssues = row.PaymentIssues,
                DriverGap = row.DriverGap,
                Score = (row.LateOrders * 4) + (row.PaymentIssues * 5) + (row.DriverGap * 3),
                Route = "/orders"
            })
            .OrderByDescending(row => row.Score)
            .Take(6)
            .ToList();
    }

    private static IReadOnlyList<AdminDashboardDistributionBucketDto> BuildVendorReadiness(IReadOnlyList<VendorRow> vendors)
    {
        var total = Math.Max(1, vendors.Count);
        var ready = vendors.Count(v => v.Status == VendorStatus.Active && v.AcceptOrders && !v.LockedAtUtc.HasValue);
        var review = vendors.Count(v => v.Status == VendorStatus.PendingReview);
        var blocked = vendors.Count - ready - review;

        return
        [
            BuildBucket("vendors_ready", "DASHBOARD.SUPPLY.VENDORS_VERIFIED", ready, total, "#10B981", "success"),
            BuildBucket("vendors_review", "DASHBOARD.SUPPLY.VENDORS_UNDER_REVIEW", review, total, "#F59E0B", "warning"),
            BuildBucket("vendors_blocked", "DASHBOARD.SUPPLY.VENDORS_BLOCKED", blocked, total, "#EF4444", "critical")
        ];
    }

    private static IReadOnlyList<AdminDashboardDistributionBucketDto> BuildDriverReadiness(IReadOnlyList<DriverRow> drivers)
    {
        var total = Math.Max(1, drivers.Count);
        var ready = drivers.Count(d => ResolveDriverReadiness(d) == "ready");
        var limited = drivers.Count(d => ResolveDriverReadiness(d) == "limited");
        var blocked = drivers.Count(d => ResolveDriverReadiness(d) == "blocked");

        return
        [
            BuildBucket("drivers_ready", "DASHBOARD.SUPPLY.DRIVERS_READY", ready, total, "#10B981", "success"),
            BuildBucket("drivers_limited", "DASHBOARD.SUPPLY.DRIVERS_LIMITED", limited, total, "#F59E0B", "warning"),
            BuildBucket("drivers_blocked", "DASHBOARD.SUPPLY.DRIVERS_BLOCKED", blocked, total, "#EF4444", "critical")
        ];
    }

    private static AdminDashboardDistributionBucketDto BuildBucket(string id, string labelKey, int count, int total, string color, string severity) =>
        new()
        {
            Id = id,
            LabelKey = labelKey,
            Count = count,
            Share = Math.Round((decimal)count / total * 100m, 1),
            Color = color,
            Severity = severity
        };

    private static IReadOnlyList<AdminDashboardQueueDto> BuildLiveQueues(IReadOnlyList<OrderRow> orders)
    {
        return
        [
            new AdminDashboardQueueDto
            {
                Id = "preparation",
                LabelKey = "DASHBOARD.QUEUES.PREPARATION",
                Count = orders.Count(o => o.Status is OrderStatus.Accepted or OrderStatus.Preparing or OrderStatus.ReadyForPickup),
                HelperKey = "DASHBOARD.QUEUE_HELPERS.PREPARATION",
                Severity = "warning",
                Route = "/orders"
            },
            new AdminDashboardQueueDto
            {
                Id = "dispatch",
                LabelKey = "DASHBOARD.QUEUES.DISPATCH",
                Count = orders.Count(o => o.Status is OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned or OrderStatus.PickedUp or OrderStatus.OnTheWay),
                HelperKey = "DASHBOARD.QUEUE_HELPERS.DISPATCH",
                Severity = "info",
                Route = "/orders"
            },
            new AdminDashboardQueueDto
            {
                Id = "late_orders",
                LabelKey = "DASHBOARD.QUEUES.LATE_ORDERS",
                Count = orders.Count(o => o.Status is OrderStatus.Preparing or OrderStatus.ReadyForPickup or OrderStatus.DriverAssigned or OrderStatus.OnTheWay),
                HelperKey = "DASHBOARD.QUEUE_HELPERS.LATE_ORDERS",
                Severity = "critical",
                Route = "/orders"
            },
            new AdminDashboardQueueDto
            {
                Id = "payment_review",
                LabelKey = "DASHBOARD.QUEUES.PAYMENT_REVIEW",
                Count = orders.Count(o => o.PaymentStatus is PaymentStatus.Failed or PaymentStatus.Pending or PaymentStatus.PendingCollection),
                HelperKey = "DASHBOARD.QUEUE_HELPERS.PAYMENT_REVIEW",
                Severity = "warning",
                Route = "/finances"
            }
        ];
    }

    private static IReadOnlyList<AdminDashboardQueueDto> BuildRiskQueues(IReadOnlyList<SupportCaseRow> supportCases, int vendorBacklog, int driverBacklog)
    {
        return
        [
            new AdminDashboardQueueDto
            {
                Id = "open_disputes",
                LabelKey = "DASHBOARD.RISK_QUEUES.OPEN_DISPUTES",
                Count = supportCases.Count(c => c.Status is not (OrderSupportCaseStatus.Resolved or OrderSupportCaseStatus.Rejected)),
                HelperKey = "DASHBOARD.RISK_HELPERS.OPEN_DISPUTES",
                Severity = "critical",
                Route = "/disputes"
            },
            new AdminDashboardQueueDto
            {
                Id = "vendor_backlog",
                LabelKey = "DASHBOARD.RISK_QUEUES.VENDOR_BACKLOG",
                Count = vendorBacklog,
                HelperKey = "DASHBOARD.RISK_HELPERS.VENDOR_BACKLOG",
                Severity = vendorBacklog > 0 ? "warning" : "success",
                Route = "/vendors"
            },
            new AdminDashboardQueueDto
            {
                Id = "customer_review",
                LabelKey = "DASHBOARD.RISK_QUEUES.CUSTOMER_REVIEW",
                Count = supportCases.Count(c => c.AwaitingResponseFromRole == "customer"),
                HelperKey = "DASHBOARD.RISK_HELPERS.CUSTOMER_REVIEW",
                Severity = "info",
                Route = "/customers"
            },
            new AdminDashboardQueueDto
            {
                Id = "driver_holds",
                LabelKey = "DASHBOARD.RISK_QUEUES.DRIVER_HOLDS",
                Count = driverBacklog,
                HelperKey = "DASHBOARD.RISK_HELPERS.DRIVER_HOLDS",
                Severity = driverBacklog > 0 ? "warning" : "success",
                Route = "/drivers"
            }
        ];
    }

    private static IReadOnlyList<AdminDashboardAlertDto> BuildAlerts(
        int paymentIssues,
        IReadOnlyList<SupportCaseRow> supportCases,
        int vendorBacklog,
        int driverBacklog,
        IReadOnlyList<AdminDashboardRegionPressureDto> regionPressure)
    {
        var items = new List<AdminDashboardAlertDto>();
        if (paymentIssues > 0)
        {
            items.Add(new AdminDashboardAlertDto
            {
                Id = "payment_failures",
                Severity = "critical",
                TitleKey = "DASHBOARD.ALERTS.PAYMENT_FAILURE.TITLE",
                SummaryKey = "DASHBOARD.ALERTS.PAYMENT_FAILURE.SUMMARY",
                SummaryParams = new Dictionary<string, object?> { ["count"] = paymentIssues, ["scope"] = regionPressure.FirstOrDefault()?.RegionLabel ?? "كل الشبكة" },
                Count = paymentIssues,
                Route = "/finances"
            });
        }

        var criticalCases = supportCases.Count(c => c.Priority == OrderSupportCasePriority.Critical && c.Status is not (OrderSupportCaseStatus.Resolved or OrderSupportCaseStatus.Rejected));
        if (criticalCases > 0)
        {
            items.Add(new AdminDashboardAlertDto
            {
                Id = "critical_disputes",
                Severity = "critical",
                TitleKey = "DASHBOARD.ALERTS.DISPUTE_CLUSTER.TITLE",
                SummaryKey = "DASHBOARD.ALERTS.DISPUTE_CLUSTER.SUMMARY",
                SummaryParams = new Dictionary<string, object?> { ["count"] = criticalCases, ["exposure"] = supportCases.Where(c => c.Priority == OrderSupportCasePriority.Critical).Sum(c => c.RequestedRefundAmount ?? 0m).ToString("N0") },
                Count = criticalCases,
                Route = "/disputes"
            });
        }

        if (vendorBacklog + driverBacklog > 0)
        {
            items.Add(new AdminDashboardAlertDto
            {
                Id = "supply_backlog",
                Severity = "warning",
                TitleKey = "DASHBOARD.ALERTS.SUPPLY_BACKLOG.TITLE",
                SummaryKey = "DASHBOARD.ALERTS.SUPPLY_BACKLOG.SUMMARY",
                SummaryParams = new Dictionary<string, object?> { ["count"] = vendorBacklog + driverBacklog },
                Count = vendorBacklog + driverBacklog,
                Route = "/drivers"
            });
        }

        return items;
    }

    private static IReadOnlyList<AdminDashboardAttentionItemDto> BuildAttentionItems(
        IReadOnlyList<OrderRow> orders,
        IReadOnlyList<SupportCaseRow> supportCases,
        IReadOnlyList<VendorRow> vendors,
        IReadOnlyList<DriverRow> drivers,
        IReadOnlyDictionary<Guid, VendorRow> vendorIndex)
    {
        var items = new List<AdminDashboardAttentionItemDto>();

        foreach (var order in orders.Where(IsOrderAtRisk).OrderBy(o => o.PlacedAtUtc).Take(2))
        {
            var vendorName = vendorIndex.TryGetValue(order.VendorId, out var vendor)
                ? (string.IsNullOrWhiteSpace(vendor.BusinessNameAr) ? vendor.BusinessNameEn : vendor.BusinessNameAr)
                : "Vendor";
            items.Add(new AdminDashboardAttentionItemDto
            {
                Id = $"order_{order.Id}",
                EntityLabelKey = "DASHBOARD.ENTITY.ORDER",
                EntityName = order.OrderNumber,
                Summary = order.PaymentStatus == PaymentStatus.Failed ? "فشل الدفع يحتاج إجراء من المراجع." : "الطلب خرج عن نطاق التسليم المستهدف.",
                Owner = vendorName,
                Priority = order.PaymentStatus == PaymentStatus.Failed ? "critical" : "warning",
                Route = $"/orders/{order.Id}",
                ActionLabelKey = "DASHBOARD.ACTIONS.OPEN_ORDER"
            });
        }

        foreach (var supportCase in supportCases
                     .Where(c => c.Status is not (OrderSupportCaseStatus.Resolved or OrderSupportCaseStatus.Rejected))
                     .OrderByDescending(c => c.Priority)
                     .ThenBy(c => c.CreatedAtUtc)
                     .Take(2))
        {
            items.Add(new AdminDashboardAttentionItemDto
            {
                Id = $"support_{supportCase.Id}",
                EntityLabelKey = "DASHBOARD.ENTITY.DISPUTE",
                EntityName = supportCase.Id.ToString()[..8].ToUpperInvariant(),
                Summary = $"ما زالت قائمة {TranslateDashboardToken(supportCase.Queue.ToString())} تحتفظ بهذه الحالة في مرحلة {TranslateDashboardToken(supportCase.Status.ToString())}.",
                Owner = TranslateDashboardToken(supportCase.Queue.ToString()),
                Priority = supportCase.Priority == OrderSupportCasePriority.Critical ? "critical" : "warning",
                Route = "/disputes",
                ActionLabelKey = "DASHBOARD.ACTIONS.OPEN_DISPUTES"
            });
        }

        var suspendedVendor = vendors.FirstOrDefault(v => v.Status == VendorStatus.Suspended || v.LockedAtUtc.HasValue);
        if (suspendedVendor is not null)
        {
            items.Add(new AdminDashboardAttentionItemDto
            {
                Id = $"vendor_{suspendedVendor.Id}",
                EntityLabelKey = "DASHBOARD.ENTITY.VENDOR",
                EntityName = string.IsNullOrWhiteSpace(suspendedVendor.BusinessNameAr) ? suspendedVendor.BusinessNameEn : suspendedVendor.BusinessNameAr,
                Summary = "جاهزية التاجر متوقفة وتحتاج مراجعة امتثال أو تشغيل.",
                Owner = "فريق التجار",
                Priority = "warning",
                Route = "/vendors",
                ActionLabelKey = "DASHBOARD.ACTIONS.REVIEW_VENDOR"
            });
        }

        var blockedDriver = drivers.FirstOrDefault(d => ResolveDriverReadiness(d) == "blocked");
        if (blockedDriver is not null)
        {
            items.Add(new AdminDashboardAttentionItemDto
            {
                Id = $"driver_{blockedDriver.Id}",
                EntityLabelKey = "DASHBOARD.ENTITY.DRIVER",
                EntityName = blockedDriver.UserId.ToString()[..8].ToUpperInvariant(),
                Summary = "إتاحة السائق متوقفة بسبب التحقق أو تعليق الحساب.",
                Owner = blockedDriver.City ?? blockedDriver.Region ?? "عمليات السائقين",
                Priority = "critical",
                Route = "/drivers",
                ActionLabelKey = "DASHBOARD.ACTIONS.OPEN_DRIVER"
            });
        }

        return items.Take(6).ToList();
    }

    private static AdminDashboardSectionDto BuildSystemHealthSection(
        int adminUsersCount,
        int activeUsersCount,
        int unreadNotifications,
        int pushDevicesCount,
        int alertsCount)
    {
        return new AdminDashboardSectionDto
        {
            Id = "system-health",
            TitleKey = "DASHBOARD.SECTIONS.SYSTEM_HEALTH.TITLE",
            DescriptionKey = "DASHBOARD.SECTIONS.SYSTEM_HEALTH.DESC",
            Route = "/dashboard",
            Status = new AdminDashboardSectionStatusDto
            {
                Severity = alertsCount > 0 ? "warning" : "success",
                SummaryKey = alertsCount > 0
                    ? "DASHBOARD.SECTIONS.SYSTEM_HEALTH.STATUS_ATTENTION"
                    : "DASHBOARD.SECTIONS.SYSTEM_HEALTH.STATUS_STABLE",
                SummaryParams = new Dictionary<string, object?> { ["count"] = alertsCount }
            },
            Stats =
            [
                BuildStat("admin-users", "DASHBOARD.STATS.ADMIN_USERS", adminUsersCount, adminUsersCount.ToString("N0"), "info", "DASHBOARD.STATS_HELPERS.ADMIN_USERS"),
                BuildStat("active-users", "DASHBOARD.STATS.ACTIVE_USERS", activeUsersCount, activeUsersCount.ToString("N0"), "success", "DASHBOARD.STATS_HELPERS.ACTIVE_USERS"),
                BuildStat("unread-notifications", "DASHBOARD.STATS.UNREAD_NOTIFICATIONS", unreadNotifications, unreadNotifications.ToString("N0"), unreadNotifications > 0 ? "warning" : "neutral", "DASHBOARD.STATS_HELPERS.UNREAD_NOTIFICATIONS"),
                BuildStat("push-devices", "DASHBOARD.STATS.PUSH_DEVICES", pushDevicesCount, pushDevicesCount.ToString("N0"), "neutral", "DASHBOARD.STATS_HELPERS.PUSH_DEVICES")
            ]
        };
    }

    private static AdminDashboardSectionDto BuildOrderOpsSection(
        IReadOnlyList<OrderRow> orders,
        IReadOnlyList<SupportCaseRow> supportCases,
        IReadOnlyDictionary<Guid, VendorRow> vendorIndex,
        decimal gmv,
        int lateOrders,
        int paymentIssues)
    {
        var cancellations = orders.Count(o => o.Status == OrderStatus.Cancelled);
        var avgBasket = orders.Count == 0 ? 0m : Math.Round(gmv / orders.Count, 1);
        var topVendors = orders
            .GroupBy(o => o.VendorId)
            .Select(group => new
            {
                VendorId = group.Key,
                Count = group.Count(),
                Risk = group.Count(IsOrderAtRisk)
            })
            .OrderByDescending(item => item.Count)
            .Take(5)
            .Select(item =>
            {
                var label = vendorIndex.TryGetValue(item.VendorId, out var vendor)
                    ? (string.IsNullOrWhiteSpace(vendor.BusinessNameAr) ? vendor.BusinessNameEn : vendor.BusinessNameAr)
                    : "تاجر";
                return new AdminDashboardRankedRowDto
                {
                    Id = $"orders_vendor_{item.VendorId}",
                    Label = label,
                    Value = item.Count.ToString("N0"),
                    SecondaryValue = $"{item.Risk:N0} مخاطر",
                    Severity = item.Risk > 0 ? "warning" : "success",
                    Route = "/orders"
                };
            })
            .ToList();

        var exceptions = orders
            .Where(IsOrderAtRisk)
            .OrderBy(o => o.PlacedAtUtc)
            .Take(5)
            .Select(order => new AdminDashboardExceptionRowDto
            {
                Id = $"order_exception_{order.Id}",
                EntityLabel = order.OrderNumber,
                IssueLabel = order.PaymentStatus == PaymentStatus.Failed
                    ? "فشل الدفع"
                    : "مسار التسليم خارج النطاق المستهدف",
                OwnerLabel = vendorIndex.TryGetValue(order.VendorId, out var vendor)
                    ? (string.IsNullOrWhiteSpace(vendor.BusinessNameAr) ? vendor.BusinessNameEn : vendor.BusinessNameAr)
                    : "تاجر",
                MetricLabel = order.TotalAmount.ToString("N0"),
                Severity = order.PaymentStatus == PaymentStatus.Failed ? "critical" : "warning",
                Route = $"/orders/{order.Id}"
            })
            .ToList();

        return new AdminDashboardSectionDto
        {
            Id = "order-ops",
            TitleKey = "DASHBOARD.SECTIONS.ORDER_OPS.TITLE",
            DescriptionKey = "DASHBOARD.SECTIONS.ORDER_OPS.DESC",
            Route = "/orders",
            Status = new AdminDashboardSectionStatusDto
            {
                Severity = lateOrders + paymentIssues > 0 ? "warning" : "success",
                SummaryKey = "DASHBOARD.SECTIONS.ORDER_OPS.STATUS",
                SummaryParams = new Dictionary<string, object?> { ["late"] = lateOrders, ["payment"] = paymentIssues }
            },
            Stats =
            [
                BuildStat("total-orders", "DASHBOARD.STATS.TOTAL_ORDERS", orders.Count, orders.Count.ToString("N0"), "info", "DASHBOARD.STATS_HELPERS.TOTAL_ORDERS"),
                BuildStat("late-orders", "DASHBOARD.STATS.LATE_ORDERS", lateOrders, lateOrders.ToString("N0"), lateOrders > 0 ? "warning" : "success", "DASHBOARD.STATS_HELPERS.LATE_ORDERS"),
                BuildStat("cancellations", "DASHBOARD.STATS.CANCELLATIONS", cancellations, cancellations.ToString("N0"), cancellations > 0 ? "warning" : "neutral", "DASHBOARD.STATS_HELPERS.CANCELLATIONS"),
                BuildStat("avg-basket", "DASHBOARD.STATS.AVG_BASKET", avgBasket, avgBasket.ToString("N1"), "neutral", "DASHBOARD.STATS_HELPERS.AVG_BASKET", "ر.س")
            ],
            RankedLists =
            [
                new AdminDashboardRankedListDto
                {
                    Id = "top-vendors-by-orders",
                    TitleKey = "DASHBOARD.RANKINGS.TOP_VENDORS_ORDERS",
                    DescriptionKey = "DASHBOARD.RANKINGS.TOP_VENDORS_ORDERS_DESC",
                    Rows = topVendors
                },
                new AdminDashboardRankedListDto
                {
                    Id = "support-pressure",
                    TitleKey = "DASHBOARD.RANKINGS.SUPPORT_PRESSURE",
                    DescriptionKey = "DASHBOARD.RANKINGS.SUPPORT_PRESSURE_DESC",
                    Rows = supportCases
                        .GroupBy(c => c.Queue.ToString())
                        .Select(group => new AdminDashboardRankedRowDto
                        {
                            Id = $"queue_{group.Key}",
                            Label = TranslateDashboardToken(group.Key),
                            Value = group.Count().ToString("N0"),
                            SecondaryValue = $"{group.Count(c => c.Priority == OrderSupportCasePriority.Critical):N0} حرجة",
                            Severity = group.Any(c => c.Priority == OrderSupportCasePriority.Critical) ? "critical" : "info",
                            Route = "/disputes"
                        })
                        .OrderByDescending(row => int.Parse(row.Value.Replace(",", string.Empty)))
                        .ToList()
                }
            ],
            Exceptions = exceptions
        };
    }

    private static AdminDashboardSectionDto BuildVendorOpsSection(
        IReadOnlyList<VendorRow> vendors,
        IReadOnlyList<OrderRow> orders,
        IReadOnlyList<SupportCaseRow> supportCases,
        int pendingDocumentReviews,
        int vendorBranchesCount,
        IReadOnlyDictionary<Guid, VendorRow> vendorIndex,
        DashboardGeographyScope geography)
    {
        var activeVendors = vendors.Count(v => v.Status == VendorStatus.Active);
        var pendingVendors = vendors.Count(v => v.Status == VendorStatus.PendingReview);
        var suspendedVendors = vendors.Count(v => v.Status == VendorStatus.Suspended || v.LockedAtUtc.HasValue);
        var topIssues = supportCases
            .Where(c => c.OrderId.HasValue)
            .Join(orders, c => c.OrderId!.Value, o => o.Id, (c, o) => new { SupportCase = c, o.VendorId })
            .GroupBy(item => item.VendorId)
            .Select(group => new
            {
                VendorId = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .Take(5)
            .ToList();

        return new AdminDashboardSectionDto
        {
            Id = "vendor-ops",
            TitleKey = "DASHBOARD.SECTIONS.VENDOR_OPS.TITLE",
            DescriptionKey = "DASHBOARD.SECTIONS.VENDOR_OPS.DESC",
            Route = "/vendors",
            Status = new AdminDashboardSectionStatusDto
            {
                Severity = pendingDocumentReviews > 0 || suspendedVendors > 0 ? "warning" : "success",
                SummaryKey = "DASHBOARD.SECTIONS.VENDOR_OPS.STATUS",
                SummaryParams = new Dictionary<string, object?> { ["pending"] = pendingDocumentReviews, ["blocked"] = suspendedVendors }
            },
            Stats =
            [
                BuildStat("active-vendors", "DASHBOARD.STATS.ACTIVE_VENDORS", activeVendors, activeVendors.ToString("N0"), "success", "DASHBOARD.STATS_HELPERS.ACTIVE_VENDORS"),
                BuildStat("pending-vendors", "DASHBOARD.STATS.PENDING_VENDORS", pendingVendors, pendingVendors.ToString("N0"), pendingVendors > 0 ? "warning" : "neutral", "DASHBOARD.STATS_HELPERS.PENDING_VENDORS"),
                BuildStat("blocked-vendors", "DASHBOARD.STATS.BLOCKED_VENDORS", suspendedVendors, suspendedVendors.ToString("N0"), suspendedVendors > 0 ? "critical" : "success", "DASHBOARD.STATS_HELPERS.BLOCKED_VENDORS"),
                BuildStat("branches", "DASHBOARD.STATS.VENDOR_BRANCHES", vendorBranchesCount, vendorBranchesCount.ToString("N0"), "neutral", "DASHBOARD.STATS_HELPERS.VENDOR_BRANCHES")
            ],
            RankedLists =
            [
                new AdminDashboardRankedListDto
                {
                    Id = "vendor-issue-share",
                    TitleKey = "DASHBOARD.RANKINGS.VENDOR_ISSUE_SHARE",
                    DescriptionKey = "DASHBOARD.RANKINGS.VENDOR_ISSUE_SHARE_DESC",
                    Rows = topIssues.Select(item => new AdminDashboardRankedRowDto
                    {
                        Id = $"vendor_issue_{item.VendorId}",
                        Label = vendorIndex.TryGetValue(item.VendorId, out var vendor)
                            ? (string.IsNullOrWhiteSpace(vendor.BusinessNameAr) ? vendor.BusinessNameEn : vendor.BusinessNameAr)
                            : "تاجر",
                        Value = item.Count.ToString("N0"),
                        Severity = item.Count >= 3 ? "warning" : "info",
                        Route = "/vendors"
                    }).ToList()
                }
            ],
            Exceptions = vendors
                .Where(v => v.Status == VendorStatus.Suspended || v.LockedAtUtc.HasValue || !v.AcceptOrders)
                .Take(5)
                .Select(v => new AdminDashboardExceptionRowDto
                {
                    Id = $"vendor_exception_{v.Id}",
                EntityLabel = string.IsNullOrWhiteSpace(v.BusinessNameAr) ? v.BusinessNameEn : v.BusinessNameAr,
                    IssueLabel = v.Status == VendorStatus.Suspended || v.LockedAtUtc.HasValue ? "التاجر محظور" : "استقبال الطلبات متوقف حاليًا",
                    OwnerLabel = geography.GetRegionLabel(geography.ResolveEntityRegionCode(v.City, v.Region)),
                    MetricLabel = TranslateDashboardToken(v.Status.ToString()),
                    Severity = v.Status == VendorStatus.Suspended || v.LockedAtUtc.HasValue ? "critical" : "warning",
                    Route = "/vendors"
                })
                .ToList()
        };
    }

    private static AdminDashboardSectionDto BuildDriverOpsSection(
        IReadOnlyList<DriverRow> drivers,
        IReadOnlyList<AdminDashboardRegionPressureDto> regionPressure,
        int openDriverIncidents,
        int pendingWithdrawals,
        int processingWithdrawals)
    {
        var ready = drivers.Count(d => ResolveDriverReadiness(d) == "ready");
        var limited = drivers.Count(d => ResolveDriverReadiness(d) == "limited");
        var blocked = drivers.Count(d => ResolveDriverReadiness(d) == "blocked");

        return new AdminDashboardSectionDto
        {
            Id = "driver-ops",
            TitleKey = "DASHBOARD.SECTIONS.DRIVER_OPS.TITLE",
            DescriptionKey = "DASHBOARD.SECTIONS.DRIVER_OPS.DESC",
            Route = "/drivers",
            Status = new AdminDashboardSectionStatusDto
            {
                Severity = blocked > 0 || openDriverIncidents > 0 ? "warning" : "success",
                SummaryKey = "DASHBOARD.SECTIONS.DRIVER_OPS.STATUS",
                SummaryParams = new Dictionary<string, object?> { ["blocked"] = blocked, ["incidents"] = openDriverIncidents }
            },
            Stats =
            [
                BuildStat("ready-drivers", "DASHBOARD.STATS.READY_DRIVERS", ready, ready.ToString("N0"), "success", "DASHBOARD.STATS_HELPERS.READY_DRIVERS"),
                BuildStat("limited-drivers", "DASHBOARD.STATS.LIMITED_DRIVERS", limited, limited.ToString("N0"), limited > 0 ? "warning" : "neutral", "DASHBOARD.STATS_HELPERS.LIMITED_DRIVERS"),
                BuildStat("blocked-drivers", "DASHBOARD.STATS.BLOCKED_DRIVERS", blocked, blocked.ToString("N0"), blocked > 0 ? "critical" : "success", "DASHBOARD.STATS_HELPERS.BLOCKED_DRIVERS"),
                BuildStat("driver-incidents", "DASHBOARD.STATS.DRIVER_INCIDENTS", openDriverIncidents, openDriverIncidents.ToString("N0"), openDriverIncidents > 0 ? "warning" : "neutral", "DASHBOARD.STATS_HELPERS.DRIVER_INCIDENTS")
            ],
            RankedLists =
            [
                new AdminDashboardRankedListDto
                {
                    Id = "regional-supply-gap",
                    TitleKey = "DASHBOARD.RANKINGS.REGIONAL_SUPPLY_GAP",
                    DescriptionKey = "DASHBOARD.RANKINGS.REGIONAL_SUPPLY_GAP_DESC",
                    Rows = regionPressure.Take(5).Select(region => new AdminDashboardRankedRowDto
                    {
                        Id = $"driver_gap_{region.RegionKey}",
                        Label = region.RegionLabel,
                        Value = region.Score.ToString("N0"),
                        SecondaryValue = $"{region.DriverGap:N0} فجوة",
                    MetaLabel = $"{region.LateOrders:N0} متأخرة / {region.PaymentIssues:N0} دفع",
                        Severity = region.Score >= 15 ? "critical" : region.Score >= 8 ? "warning" : "info",
                        Route = "/drivers"
                    }).ToList()
                }
            ],
            Exceptions =
            [
                new AdminDashboardExceptionRowDto
                {
                    Id = "driver-finance-pending",
                    EntityLabel = "سحوبات السائقين",
                    IssueLabel = "مراجعة صرف معلقة",
                    OwnerLabel = "مالية السائقين",
                    MetricLabel = pendingWithdrawals.ToString("N0"),
                    Severity = pendingWithdrawals > 0 ? "warning" : "success",
                    Route = "/drivers"
                },
                new AdminDashboardExceptionRowDto
                {
                    Id = "driver-finance-processing",
                    EntityLabel = "سحوبات السائقين",
                    IssueLabel = "دفعة صرف قيد المعالجة",
                    OwnerLabel = "مالية السائقين",
                    MetricLabel = processingWithdrawals.ToString("N0"),
                    Severity = processingWithdrawals > 0 ? "info" : "neutral",
                    Route = "/drivers"
                }
            ]
        };
    }

    private static AdminDashboardSectionDto BuildCustomerSupportSection(
        int customersTotal,
        int newCustomers,
        int activeCustomers,
        IReadOnlyList<SupportCaseRow> supportCases)
    {
        var criticalSupport = supportCases.Count(c => c.Priority == OrderSupportCasePriority.Critical && c.Status is not (OrderSupportCaseStatus.Resolved or OrderSupportCaseStatus.Rejected));

        return new AdminDashboardSectionDto
        {
            Id = "customer-support",
            TitleKey = "DASHBOARD.SECTIONS.CUSTOMER_SUPPORT.TITLE",
            DescriptionKey = "DASHBOARD.SECTIONS.CUSTOMER_SUPPORT.DESC",
            Route = "/customers",
            Status = new AdminDashboardSectionStatusDto
            {
                Severity = criticalSupport > 0 ? "warning" : "success",
                SummaryKey = "DASHBOARD.SECTIONS.CUSTOMER_SUPPORT.STATUS",
                SummaryParams = new Dictionary<string, object?> { ["critical"] = criticalSupport, ["reviews"] = 0 }
            },
            Stats =
            [
                BuildStat("customers-total", "DASHBOARD.STATS.CUSTOMERS_TOTAL", customersTotal, customersTotal.ToString("N0"), "neutral", "DASHBOARD.STATS_HELPERS.CUSTOMERS_TOTAL"),
                BuildStat("customers-new", "DASHBOARD.STATS.CUSTOMERS_NEW", newCustomers, newCustomers.ToString("N0"), "info", "DASHBOARD.STATS_HELPERS.CUSTOMERS_NEW"),
                BuildStat("customers-active", "DASHBOARD.STATS.CUSTOMERS_ACTIVE", activeCustomers, activeCustomers.ToString("N0"), "success", "DASHBOARD.STATS_HELPERS.CUSTOMERS_ACTIVE")
            ],
            RankedLists =
            [
                new AdminDashboardRankedListDto
                {
                    Id = "support-queue-aging",
                    TitleKey = "DASHBOARD.RANKINGS.SUPPORT_QUEUE_AGING",
                    DescriptionKey = "DASHBOARD.RANKINGS.SUPPORT_QUEUE_AGING_DESC",
                    Rows = supportCases
                        .Where(c => c.Status is not (OrderSupportCaseStatus.Resolved or OrderSupportCaseStatus.Rejected))
                        .GroupBy(c => c.Queue.ToString())
                        .Select(group => new AdminDashboardRankedRowDto
                        {
                            Id = $"support_{group.Key}",
                            Label = TranslateDashboardToken(group.Key),
                            Value = group.Count().ToString("N0"),
                            SecondaryValue = $"{group.Count(c => c.Priority == OrderSupportCasePriority.Critical):N0} حرجة",
                            Severity = group.Any(c => c.Priority == OrderSupportCasePriority.Critical) ? "critical" : "warning",
                            Route = "/disputes"
                        })
                        .OrderByDescending(row => int.Parse(row.Value.Replace(",", string.Empty)))
                        .ToList()
                }
            ],
            Exceptions = []
        };
    }

    private static AdminDashboardSectionDto BuildFinanceOpsSection(
        decimal gmv,
        decimal refundsTotal,
        int refundsCount,
        int paymentsFailedCount,
        int paymentsPendingCount,
        int pendingSettlements,
        int failedSettlements,
        decimal settledNetAmount,
        int walletsCount,
        decimal walletInflow,
        decimal walletOutflow)
    {
        return new AdminDashboardSectionDto
        {
            Id = "finance-ops",
            TitleKey = "DASHBOARD.SECTIONS.FINANCE_OPS.TITLE",
            DescriptionKey = "DASHBOARD.SECTIONS.FINANCE_OPS.DESC",
            Route = "/finances",
            Status = new AdminDashboardSectionStatusDto
            {
                Severity = paymentsFailedCount + pendingSettlements + failedSettlements > 0 ? "warning" : "success",
                SummaryKey = "DASHBOARD.SECTIONS.FINANCE_OPS.STATUS",
                SummaryParams = new Dictionary<string, object?> { ["failed"] = paymentsFailedCount, ["pending"] = pendingSettlements }
            },
            Stats =
            [
                BuildStat("gmv-section", "DASHBOARD.STATS.FINANCE_GMV", gmv, Math.Round(gmv, 0).ToString("N0"), "info", "DASHBOARD.STATS_HELPERS.FINANCE_GMV", "ر.س"),
                BuildStat("refunds-total", "DASHBOARD.STATS.REFUNDS_TOTAL", refundsTotal, Math.Round(refundsTotal, 0).ToString("N0"), refundsTotal > 0 ? "warning" : "success", "DASHBOARD.STATS_HELPERS.REFUNDS_TOTAL", "ر.س"),
                BuildStat("settled-net", "DASHBOARD.STATS.SETTLED_NET", settledNetAmount, Math.Round(settledNetAmount, 0).ToString("N0"), "success", "DASHBOARD.STATS_HELPERS.SETTLED_NET", "ر.س"),
                BuildStat("wallets-count", "DASHBOARD.STATS.WALLETS_COUNT", walletsCount, walletsCount.ToString("N0"), "neutral", "DASHBOARD.STATS_HELPERS.WALLETS_COUNT")
            ],
            RankedLists =
            [
                new AdminDashboardRankedListDto
                {
                    Id = "finance-watchlist",
                    TitleKey = "DASHBOARD.RANKINGS.FINANCE_WATCHLIST",
                    DescriptionKey = "DASHBOARD.RANKINGS.FINANCE_WATCHLIST_DESC",
                    Rows =
                    [
                        new AdminDashboardRankedRowDto { Id = "payments-failed", Label = "مدفوعات فاشلة", Value = paymentsFailedCount.ToString("N0"), Severity = paymentsFailedCount > 0 ? "critical" : "success", Route = "/finances" },
                        new AdminDashboardRankedRowDto { Id = "payments-pending", Label = "مدفوعات معلقة", Value = paymentsPendingCount.ToString("N0"), Severity = paymentsPendingCount > 0 ? "warning" : "neutral", Route = "/finances" },
                        new AdminDashboardRankedRowDto { Id = "settlements-pending", Label = "تسويات معلقة", Value = pendingSettlements.ToString("N0"), Severity = pendingSettlements > 0 ? "warning" : "neutral", Route = "/wallets" },
                        new AdminDashboardRankedRowDto { Id = "settlements-failed", Label = "تسويات فاشلة", Value = failedSettlements.ToString("N0"), Severity = failedSettlements > 0 ? "critical" : "success", Route = "/wallets" }
                    ]
                }
            ],
            Exceptions =
            [
                new AdminDashboardExceptionRowDto
                {
                    Id = "wallet-flow-in",
                    EntityLabel = "تدفق داخل للمحفظة",
                    IssueLabel = "تدفق داخل مرصود خلال الفترة المحددة",
                    OwnerLabel = "المحافظ",
                    MetricLabel = $"{Math.Round(walletInflow, 0):N0} ر.س",
                    Severity = "success",
                    Route = "/wallets"
                },
                new AdminDashboardExceptionRowDto
                {
                    Id = "wallet-flow-out",
                    EntityLabel = "تدفق خارج من المحفظة",
                    IssueLabel = "تدفق خارج مرصود خلال الفترة المحددة",
                    OwnerLabel = "المحافظ",
                    MetricLabel = $"{Math.Round(walletOutflow, 0):N0} ر.س",
                    Severity = walletOutflow > walletInflow ? "warning" : "info",
                    Route = "/wallets"
                },
                new AdminDashboardExceptionRowDto
                {
                    Id = "refunds-count",
                    EntityLabel = "عمليات الاسترداد",
                    IssueLabel = "حالات استرداد تم إنشاؤها خلال الفترة المحددة",
                    OwnerLabel = "المالية",
                    MetricLabel = refundsCount.ToString("N0"),
                    Severity = refundsCount > 0 ? "warning" : "success",
                    Route = "/finances"
                }
            ]
        };
    }

    private static AdminDashboardSectionDto BuildCatalogHealthSection(
        int productsCount,
        int brandsCount,
        int categoriesCount,
        IReadOnlyList<VendorProductRow> vendorProducts,
        int pendingProductRequests,
        int pendingBrandRequests,
        int pendingCategoryRequests,
        int lowStockProducts,
        int unavailableProducts,
        IReadOnlyDictionary<Guid, VendorRow> vendorIndex)
    {
        return new AdminDashboardSectionDto
        {
            Id = "catalog-health",
            TitleKey = "DASHBOARD.SECTIONS.CATALOG_HEALTH.TITLE",
            DescriptionKey = "DASHBOARD.SECTIONS.CATALOG_HEALTH.DESC",
            Route = "/catalog/products",
            Status = new AdminDashboardSectionStatusDto
            {
                Severity = pendingProductRequests + pendingBrandRequests + pendingCategoryRequests > 0 ? "warning" : "success",
                SummaryKey = "DASHBOARD.SECTIONS.CATALOG_HEALTH.STATUS",
                SummaryParams = new Dictionary<string, object?> { ["requests"] = pendingProductRequests + pendingBrandRequests + pendingCategoryRequests }
            },
            Stats =
            [
                BuildStat("master-products", "DASHBOARD.STATS.MASTER_PRODUCTS", productsCount, productsCount.ToString("N0"), "info", "DASHBOARD.STATS_HELPERS.MASTER_PRODUCTS"),
                BuildStat("brands", "DASHBOARD.STATS.BRANDS", brandsCount, brandsCount.ToString("N0"), "neutral", "DASHBOARD.STATS_HELPERS.BRANDS"),
                BuildStat("categories", "DASHBOARD.STATS.CATEGORIES", categoriesCount, categoriesCount.ToString("N0"), "neutral", "DASHBOARD.STATS_HELPERS.CATEGORIES"),
                BuildStat("low-stock", "DASHBOARD.STATS.LOW_STOCK", lowStockProducts, lowStockProducts.ToString("N0"), lowStockProducts > 0 ? "warning" : "success", "DASHBOARD.STATS_HELPERS.LOW_STOCK")
            ],
            RankedLists =
            [
                new AdminDashboardRankedListDto
                {
                    Id = "catalog-vendor-gaps",
                    TitleKey = "DASHBOARD.RANKINGS.CATALOG_VENDOR_GAPS",
                    DescriptionKey = "DASHBOARD.RANKINGS.CATALOG_VENDOR_GAPS_DESC",
                    Rows = vendorProducts
                        .GroupBy(vp => vp.VendorId)
                        .Select(group => new
                        {
                            VendorId = group.Key,
                            Gap = group.Count(item => !item.IsAvailable || item.Status is VendorProductStatus.OutOfStock or VendorProductStatus.Suspended),
                            Total = group.Count()
                        })
                        .OrderByDescending(item => item.Gap)
                        .Take(5)
                        .Select(item => new AdminDashboardRankedRowDto
                        {
                            Id = $"catalog_gap_{item.VendorId}",
                            Label = vendorIndex.TryGetValue(item.VendorId, out var vendor)
                                ? (string.IsNullOrWhiteSpace(vendor.BusinessNameAr) ? vendor.BusinessNameEn : vendor.BusinessNameAr)
                                : "تاجر",
                            Value = item.Gap.ToString("N0"),
                            SecondaryValue = $"{item.Total:N0} إجمالي",
                            Severity = item.Gap > 0 ? "warning" : "success",
                            Route = "/catalog/products"
                        })
                        .ToList()
                }
            ],
            Exceptions =
            [
                new AdminDashboardExceptionRowDto
                {
                    Id = "product-requests",
                    EntityLabel = "طلبات المنتجات",
                    IssueLabel = "موافقات كتالوج معلقة",
                    OwnerLabel = "الكتالوج",
                    MetricLabel = pendingProductRequests.ToString("N0"),
                    Severity = pendingProductRequests > 0 ? "warning" : "success",
                    Route = "/catalog/products"
                },
                new AdminDashboardExceptionRowDto
                {
                    Id = "brand-requests",
                    EntityLabel = "طلبات العلامات التجارية",
                    IssueLabel = "موافقات علامات تجارية معلقة",
                    OwnerLabel = "الكتالوج",
                    MetricLabel = pendingBrandRequests.ToString("N0"),
                    Severity = pendingBrandRequests > 0 ? "warning" : "success",
                    Route = "/catalog/brands"
                },
                new AdminDashboardExceptionRowDto
                {
                    Id = "category-requests",
                    EntityLabel = "طلبات التصنيفات",
                    IssueLabel = "موافقات تصنيفات معلقة",
                    OwnerLabel = "الكتالوج",
                    MetricLabel = pendingCategoryRequests.ToString("N0"),
                    Severity = pendingCategoryRequests > 0 ? "warning" : "success",
                    Route = "/catalog/categories"
                },
                new AdminDashboardExceptionRowDto
                {
                    Id = "unavailable-products",
                    EntityLabel = "منتجات تجار غير متاحة",
                    IssueLabel = "عناصر كتالوج غير جاهزة للبيع",
                    OwnerLabel = "الكتالوج",
                    MetricLabel = unavailableProducts.ToString("N0"),
                    Severity = unavailableProducts > 0 ? "warning" : "success",
                    Route = "/catalog/products"
                }
            ]
        };
    }

    private static AdminDashboardSectionDto BuildMarketingPulseSection(
        int activeCouponsCount,
        int activeBannersCount,
        int featuredPlacementsCount,
        int unreadNotifications,
        int recentNotifications)
    {
        return new AdminDashboardSectionDto
        {
            Id = "marketing-pulse",
            TitleKey = "DASHBOARD.SECTIONS.MARKETING_PULSE.TITLE",
            DescriptionKey = "DASHBOARD.SECTIONS.MARKETING_PULSE.DESC",
            Route = "/notifications",
            Status = new AdminDashboardSectionStatusDto
            {
                Severity = unreadNotifications > 0 ? "warning" : "success",
                SummaryKey = "DASHBOARD.SECTIONS.MARKETING_PULSE.STATUS",
                SummaryParams = new Dictionary<string, object?> { ["notifications"] = unreadNotifications, ["reviews"] = 0 }
            },
            Stats =
            [
                BuildStat("active-coupons", "DASHBOARD.STATS.ACTIVE_COUPONS", activeCouponsCount, activeCouponsCount.ToString("N0"), "success", "DASHBOARD.STATS_HELPERS.ACTIVE_COUPONS"),
                BuildStat("home-banners", "DASHBOARD.STATS.HOME_BANNERS", activeBannersCount, activeBannersCount.ToString("N0"), "info", "DASHBOARD.STATS_HELPERS.HOME_BANNERS"),
                BuildStat("featured-placements", "DASHBOARD.STATS.FEATURED_PLACEMENTS", featuredPlacementsCount, featuredPlacementsCount.ToString("N0"), "neutral", "DASHBOARD.STATS_HELPERS.FEATURED_PLACEMENTS")
            ],
            RankedLists =
            [
                new AdminDashboardRankedListDto
                {
                    Id = "engagement-pulse",
                    TitleKey = "DASHBOARD.RANKINGS.ENGAGEMENT_PULSE",
                    DescriptionKey = "DASHBOARD.RANKINGS.ENGAGEMENT_PULSE_DESC",
                    Rows =
                    [
                        new AdminDashboardRankedRowDto { Id = "recent-notifications", Label = "إشعارات حديثة", Value = recentNotifications.ToString("N0"), Severity = recentNotifications > 0 ? "info" : "neutral", Route = "/notifications" },
                        new AdminDashboardRankedRowDto { Id = "unread-notifications", Label = "إشعارات غير مقروءة", Value = unreadNotifications.ToString("N0"), Severity = unreadNotifications > 0 ? "warning" : "success", Route = "/notifications" }
                    ]
                }
            ]
        };
    }

    private static AdminDashboardSectionDto BuildAccessSecuritySection(
        int rolesCount,
        int permissionDefinitionsCount,
        int userAccessScopesCount,
        int userPermissionOverridesCount,
        int adminUsersCount,
        int lockedAdminUsersCount,
        IReadOnlyList<UserRow> adminUsers)
    {
        var elevatedPermissionVersions = adminUsers.Count(u => u.PermissionVersion > 1);
        var inactiveAdmins = adminUsers.Count(u => u.AccountStatus != AccountStatus.Active);

        return new AdminDashboardSectionDto
        {
            Id = "access-security",
            TitleKey = "DASHBOARD.SECTIONS.ACCESS_SECURITY.TITLE",
            DescriptionKey = "DASHBOARD.SECTIONS.ACCESS_SECURITY.DESC",
            Route = "/admin-users",
            Status = new AdminDashboardSectionStatusDto
            {
                Severity = lockedAdminUsersCount > 0 || inactiveAdmins > 0 ? "warning" : "success",
                SummaryKey = "DASHBOARD.SECTIONS.ACCESS_SECURITY.STATUS",
                SummaryParams = new Dictionary<string, object?> { ["locked"] = lockedAdminUsersCount, ["inactive"] = inactiveAdmins }
            },
            Stats =
            [
                BuildStat("roles-count", "DASHBOARD.STATS.ROLES_COUNT", rolesCount, rolesCount.ToString("N0"), "neutral", "DASHBOARD.STATS_HELPERS.ROLES_COUNT"),
                BuildStat("permissions-count", "DASHBOARD.STATS.PERMISSIONS_COUNT", permissionDefinitionsCount, permissionDefinitionsCount.ToString("N0"), "neutral", "DASHBOARD.STATS_HELPERS.PERMISSIONS_COUNT"),
                BuildStat("access-scopes", "DASHBOARD.STATS.ACCESS_SCOPES", userAccessScopesCount, userAccessScopesCount.ToString("N0"), "info", "DASHBOARD.STATS_HELPERS.ACCESS_SCOPES"),
                BuildStat("overrides-count", "DASHBOARD.STATS.OVERRIDES_COUNT", userPermissionOverridesCount, userPermissionOverridesCount.ToString("N0"), "warning", "DASHBOARD.STATS_HELPERS.OVERRIDES_COUNT")
            ],
            RankedLists =
            [
                new AdminDashboardRankedListDto
                {
                    Id = "admin-access-health",
                    TitleKey = "DASHBOARD.RANKINGS.ADMIN_ACCESS_HEALTH",
                    DescriptionKey = "DASHBOARD.RANKINGS.ADMIN_ACCESS_HEALTH_DESC",
                    Rows =
                    [
                        new AdminDashboardRankedRowDto { Id = "admins-total", Label = "حسابات المشرفين", Value = adminUsersCount.ToString("N0"), Severity = "neutral", Route = "/admin-users" },
                        new AdminDashboardRankedRowDto { Id = "admins-locked", Label = "حسابات مشرفين مقفلة", Value = lockedAdminUsersCount.ToString("N0"), Severity = lockedAdminUsersCount > 0 ? "warning" : "success", Route = "/admin-users" },
                        new AdminDashboardRankedRowDto { Id = "admins-versioned", Label = "تجاوزات إصدار الصلاحيات", Value = elevatedPermissionVersions.ToString("N0"), Severity = elevatedPermissionVersions > 0 ? "info" : "neutral", Route = "/admin-users" }
                    ]
                }
            ],
            Exceptions = adminUsers
                .Where(u => u.IsLoginLocked || u.AccountStatus != AccountStatus.Active)
                .Take(5)
                .Select(u => new AdminDashboardExceptionRowDto
                {
                    Id = $"admin_exception_{u.Id}",
                    EntityLabel = u.FullName,
                    IssueLabel = u.IsLoginLocked ? "تسجيل دخول المشرف مقفل" : "المشرف غير نشط",
                    OwnerLabel = TranslateDashboardToken(u.Role.ToString()),
                    MetricLabel = $"v{u.PermissionVersion}",
                    Severity = u.IsLoginLocked ? "critical" : "warning",
                    Route = "/admin-users"
                })
                .ToList()
        };
    }

    private static AdminDashboardStatCardDto BuildStat(
        string id,
        string labelKey,
        decimal value,
        string displayValue,
        string tone,
        string helperKey,
        string? unit = null) =>
        new()
        {
            Id = id,
            LabelKey = labelKey,
            Value = value,
            DisplayValue = displayValue,
            Tone = tone,
            HelperKey = helperKey,
            Unit = unit
        };

    private static IReadOnlyList<AdminDashboardAuditItemDto> BuildAuditFeed(
        IReadOnlyList<OrderRow> orders,
        IReadOnlyList<SupportCaseRow> supportCases,
        IReadOnlyList<VendorRow> vendors,
        IReadOnlyList<DriverRow> drivers)
    {
        var items = new List<AdminDashboardAuditItemDto>();

        items.AddRange(vendors
            .OrderByDescending(v => v.UpdatedAtUtc)
            .Take(2)
            .Select(v => new AdminDashboardAuditItemDto
            {
                Id = $"vendor_audit_{v.Id}",
                TitleKey = "DASHBOARD.AUDIT.VENDOR_REVIEW",
                SubtitleKey = "DASHBOARD.AUDIT.VENDOR_REVIEW_SUMMARY",
                SubtitleParams = new Dictionary<string, object?> { ["count"] = 1 },
                Severity = v.Status == VendorStatus.Active ? "success" : "warning",
                TimestampUtc = v.UpdatedAtUtc == default ? v.CreatedAtUtc : v.UpdatedAtUtc,
                Route = "/vendors"
            }));

        items.AddRange(supportCases
            .OrderByDescending(c => c.UpdatedAtUtc)
            .Take(2)
            .Select(c => new AdminDashboardAuditItemDto
            {
                Id = $"support_audit_{c.Id}",
                TitleKey = "DASHBOARD.AUDIT.DISPUTE_PULSE",
                SubtitleKey = "DASHBOARD.AUDIT.DISPUTE_PULSE_SUMMARY",
                SubtitleParams = new Dictionary<string, object?> { ["count"] = 1 },
                Severity = c.Status is OrderSupportCaseStatus.Resolved or OrderSupportCaseStatus.Rejected ? "success" : "critical",
                TimestampUtc = c.UpdatedAtUtc == default ? c.CreatedAtUtc : c.UpdatedAtUtc,
                Route = "/disputes"
            }));

        items.AddRange(orders
            .OrderByDescending(o => o.PlacedAtUtc)
            .Take(2)
            .Select(o => new AdminDashboardAuditItemDto
            {
                Id = $"order_audit_{o.Id}",
                TitleKey = "DASHBOARD.AUDIT.SYSTEM_MODE",
                SubtitleKey = o.Status == OrderStatus.Delivered
                    ? "DASHBOARD.AUDIT.SYSTEM_MODE_LIVE"
                    : "DASHBOARD.AUDIT.SYSTEM_MODE_SNAPSHOT",
                Severity = o.Status == OrderStatus.Delivered ? "success" : "info",
                TimestampUtc = o.PlacedAtUtc,
                Route = "/orders"
            }));

        items.AddRange(drivers
            .OrderByDescending(d => d.UpdatedAtUtc)
            .Take(1)
            .Select(d => new AdminDashboardAuditItemDto
            {
                Id = $"driver_audit_{d.Id}",
                TitleKey = "DASHBOARD.AUDIT.CUSTOMER_RISK",
                SubtitleKey = "DASHBOARD.AUDIT.CUSTOMER_RISK_SUMMARY",
                SubtitleParams = new Dictionary<string, object?> { ["count"] = 1 },
                Severity = ResolveDriverReadiness(d) == "ready" ? "success" : "warning",
                TimestampUtc = d.UpdatedAtUtc == default ? d.CreatedAtUtc : d.UpdatedAtUtc,
                Route = "/drivers"
            }));

        return items
            .OrderByDescending(item => item.TimestampUtc)
            .Take(6)
            .ToList();
    }

    private static bool IsOrderAtRisk(OrderRow order) =>
        order.Status is OrderStatus.Preparing or OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned or OrderStatus.OnTheWay
        || order.PaymentStatus is PaymentStatus.Failed or PaymentStatus.Pending or PaymentStatus.PendingCollection;

    private static string ResolveDriverReadiness(DriverRow driver)
    {
        if (driver.Status is AccountStatus.Suspended or AccountStatus.Banned
            || driver.VerificationStatus == DriverVerificationStatus.Rejected
            || driver.IsLocationUpdatesBlocked)
        {
            return "blocked";
        }

        if (driver.Status != AccountStatus.Active
            || driver.VerificationStatus != DriverVerificationStatus.Approved
            || !driver.IsAvailable)
        {
            return "limited";
        }

        return "ready";
    }

    private static string FormatChange(int numerator, int denominator, string direction)
    {
        if (denominator <= 0)
        {
            return direction == "up" ? "+0%" : "0%";
        }

        var pct = Math.Round((decimal)numerator / denominator * 100m, 1);
        return $"{(direction == "down" ? "-" : "+")}{pct:N1}%";
    }

    private static IReadOnlyList<TimeBucket> BuildTimeBuckets(string period, DateTime start, DateTime now)
    {
        if (period == "today")
        {
            return Enumerable.Range(0, 8)
                .Select(index =>
                {
                    var bucketStart = start.AddHours(index * 3);
                    return new TimeBucket(bucketStart, bucketStart.AddHours(3), bucketStart.ToString("HH:mm"));
                })
                .ToList();
        }

        if (period == "week")
        {
            return Enumerable.Range(0, 7)
                .Select(index =>
                {
                    var bucketStart = start.AddDays(index);
                    return new TimeBucket(bucketStart, bucketStart.AddDays(1), ResolveArabicDayLabel(bucketStart));
                })
                .ToList();
        }

        return Enumerable.Range(0, 5)
            .Select(index =>
            {
                var bucketStart = start.AddDays(index * 6);
                var bucketEnd = index == 4 ? now.AddDays(1) : bucketStart.AddDays(6);
                return new TimeBucket(bucketStart, bucketEnd, $"الأسبوع {index + 1}");
            })
            .ToList();
    }

    private static string ResolveArabicDayLabel(DateTime value) =>
        value.DayOfWeek switch
        {
            DayOfWeek.Saturday => "السبت",
            DayOfWeek.Sunday => "الأحد",
            DayOfWeek.Monday => "الاثنين",
            DayOfWeek.Tuesday => "الثلاثاء",
            DayOfWeek.Wednesday => "الأربعاء",
            DayOfWeek.Thursday => "الخميس",
            DayOfWeek.Friday => "الجمعة",
            _ => value.ToString("dd/MM")
        };

    private sealed record VendorRow(
        Guid Id,
        string BusinessNameAr,
        string BusinessNameEn,
        string? Region,
        string? City,
        VendorStatus Status,
        bool AcceptOrders,
        DateTime? LockedAtUtc,
        DateTime UpdatedAtUtc,
        DateTime CreatedAtUtc);

    private sealed record OrderRow(
        Guid Id,
        string OrderNumber,
        Guid VendorId,
        OrderStatus Status,
        PaymentStatus PaymentStatus,
        decimal TotalAmount,
        decimal CommissionAmount,
        decimal DeliveryFee,
        DateTime? DeliveredAtUtc,
        DateTime? CancelledAtUtc,
        DateTime PlacedAtUtc);

    private sealed record DriverRow(
        Guid Id,
        Guid UserId,
        string? Region,
        string? City,
        AccountStatus Status,
        DriverVerificationStatus VerificationStatus,
        bool IsAvailable,
        bool IsLocationUpdatesBlocked,
        DateTime UpdatedAtUtc,
        DateTime CreatedAtUtc);

    private sealed record UserRow(
        Guid Id,
        string FullName,
        UserRole Role,
        AccountStatus AccountStatus,
        bool IsLoginLocked,
        int PermissionVersion,
        DateTime CreatedAtUtc,
        DateTime? LastLoginAtUtc,
        DateTime? LastSeenAtUtc);

    private sealed record SupportCaseRow(
        Guid Id,
        Guid? OrderId,
        OrderSupportCaseStatus Status,
        OrderSupportCasePriority Priority,
        OrderSupportCaseQueue Queue,
        decimal? RequestedRefundAmount,
        decimal? ApprovedRefundAmount,
        string? AwaitingResponseFromRole,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        DateTime? ClosedAtUtc);

    private sealed record VendorProductRow(
        Guid Id,
        Guid VendorId,
        int StockQuantity,
        bool IsAvailable,
        VendorProductStatus Status,
        DateTime CreatedAtUtc);

    private sealed record TimeBucket(DateTime Start, DateTime End, string Label)
    {
        public bool Contains(DateTime value) => value >= Start && value < End;
    }

    private sealed class RegionAccumulator
    {
        public string RegionKey { get; init; } = string.Empty;
        public string RegionLabel { get; init; } = string.Empty;
        public int LateOrders { get; set; }
        public int PaymentIssues { get; set; }
        public int DriverGap { get; set; }
    }
}
