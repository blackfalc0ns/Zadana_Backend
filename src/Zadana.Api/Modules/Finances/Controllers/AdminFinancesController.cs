using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Common.Export;
using Zadana.Api.Localization;
using Zadana.Application.Common.Export;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.DTOs;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Finances.Queries.GetAdminFinanceDashboard;
using Zadana.Application.Modules.Finances.Queries.GetCityDeliveryPricingSettings;
using Zadana.Application.Modules.Finances.Queries.GetDeliveryPricingDefaults;
using Zadana.Application.Modules.Finances.Queries.GetRegionDeliveryPricingSettings;
using Zadana.Application.Modules.Finances.Queries.GetZoneFinanceSettings;
using Zadana.Application.Modules.Finances.Commands.UpdateCityDeliveryPricingSettings;
using Zadana.Application.Modules.Finances.Commands.UpdateDeliveryPricingDefaults;
using Zadana.Application.Modules.Finances.Commands.UpdateRegionDeliveryPricingSettings;
using Zadana.Application.Modules.Finances.Commands.UpdateZoneFinanceSettings;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Api.Modules.Finances.Controllers;

[ApiController]
[Route("api/admin/finances")]
[Authorize(Policy = "AdminOnly")]
public class AdminFinancesController(
    IMediator mediator,
    IApplicationDbContext context,
    FinancialEventPostingService financialEventPostingService,
    WalletProjectionUpdater walletProjectionUpdater,
    RevenueReconciliationService revenueReconciliationService,
    FinanceOwnerNameResolver financeOwnerNameResolver) : ApiControllerBase
{
    [HttpGet("dashboard/snapshot")]
    [ProducesResponseType(typeof(AdminFinanceDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminFinanceDashboardDto>> GetDashboardSnapshot(
        [FromQuery] string period = "month",
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminFinanceDashboardQuery(period);
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("orders/{orderId:guid}/breakdown")]
    [ProducesResponseType(typeof(AdminOrderFinancialBreakdownDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminOrderFinancialBreakdownDto>> GetOrderFinancialBreakdown(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        var isPickup = order.Fulfillment == Domain.Modules.Orders.Enums.FulfillmentType.Pickup;
        var vendorCommission = order.VendorCommissionAmount > 0
            ? order.VendorCommissionAmount
            : order.CommissionAmount;
        var driverCommission = isPickup ? 0m : order.DriverCommissionAmount;
        // Pickup has no courier; delivery fee is always 0 so payout must stay 0.
        var driverPayout = isPickup
            ? 0m
            : Math.Max(0m, order.DeliveryFee - driverCommission);
        var productNet = order.ProductNet > 0 ? order.ProductNet : Math.Max(0m, order.Subtotal - order.DiscountTotal);
        var vendorEarnings = Math.Max(0m, productNet - vendorCommission);
        var platformRevenue = Math.Round(
            vendorCommission + driverCommission + (isPickup ? 0m : order.CodFee),
            2);
        var total = order.TotalAmount;
        // Customer VAT is collected on the order total — it is not a platform cost.
        // Net margin is platform take (commission / fees), not revenue minus VAT.
        var netMargin = platformRevenue;
        var marginPercent = total > 0 ? Math.Round((netMargin / total) * 100m, 2) : 0m;

        return Ok(new AdminOrderFinancialBreakdownDto(
            order.Id,
            order.OrderNumber,
            order.Subtotal,
            order.DiscountTotal,
            order.DiscountTotal,
            isPickup ? 0m : order.DeliveryFee,
            0m,
            isPickup ? 0m : order.CodFee,
            order.VatAmount,
            total,
            vendorEarnings,
            vendorCommission,
            driverPayout,
            platformRevenue,
            netMargin,
            marginPercent,
            order.Fulfillment.ToString()));
    }

    [HttpGet("pricing-settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ZoneFinanceSettingsDto>>> GetPricingSettings(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetZoneFinanceSettingsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("city-pricing")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CityDeliveryPricingSettingsDto>>> GetCityPricing(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCityDeliveryPricingSettingsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("region-pricing")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RegionDeliveryPricingSettingsDto>>> GetRegionPricing(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetRegionDeliveryPricingSettingsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("delivery-defaults")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeliveryPricingDefaultsDto>> GetDeliveryDefaults(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetDeliveryPricingDefaultsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPut("pricing-settings/{zoneId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ZoneFinanceSettingsDto>> UpdatePricingSettings(
        [FromRoute] Guid zoneId,
        [FromBody] UpdateZoneFinanceSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateZoneFinanceSettingsCommand(
            zoneId,
            request.VatPercent,
            request.CodFeeType,
            request.CodFlatFee,
            request.CodPercent,
            request.IsVatActive,
            request.IsCodFeeActive);
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPut("city-pricing/{cityId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CityDeliveryPricingSettingsDto>> UpdateCityPricing(
        [FromRoute] Guid cityId,
        [FromBody] UpdateDeliveryPricingRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCityDeliveryPricingSettingsCommand(
            cityId, request.BaseDeliveryFee, request.IncludedKm, request.ExtraKmFee,
            request.MinDeliveryFee, request.MaxDeliveryFee, request.IsPricingActive,
            request.VatPercent, request.CodFeeType, request.CodFlatFee, request.CodPercent,
            request.IsVatActive, request.IsCodFeeActive);
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPut("region-pricing/{regionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RegionDeliveryPricingSettingsDto>> UpdateRegionPricing(
        [FromRoute] Guid regionId,
        [FromBody] UpdateDeliveryPricingRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRegionDeliveryPricingSettingsCommand(
            regionId, request.BaseDeliveryFee, request.IncludedKm, request.ExtraKmFee,
            request.MinDeliveryFee, request.MaxDeliveryFee, request.IsPricingActive,
            request.VatPercent, request.CodFeeType, request.CodFlatFee, request.CodPercent,
            request.IsVatActive, request.IsCodFeeActive);
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPut("delivery-defaults")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeliveryPricingDefaultsDto>> UpdateDeliveryDefaults(
        [FromBody] UpdateDeliveryPricingDefaultsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateDeliveryPricingDefaultsCommand(
            request.Id, request.BaseDeliveryFee, request.IncludedKm, request.ExtraKmFee,
            request.MinDeliveryFee, request.MaxDeliveryFee, request.IsPricingActive,
            request.VatPercent, request.CodFeeType, request.CodFlatFee, request.CodPercent,
            request.IsVatActive, request.IsCodFeeActive, request.MinTotalDeliveryFee,
            request.MaxTotalDeliveryFee, request.MaxQuotedDistanceKm,
            request.WarningSubtotalRatioThreshold);
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("ledger")]
    [ProducesResponseType(typeof(AdminLedgerEntryListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminLedgerEntryListDto>> GetLedger(
        [FromQuery] Guid? orderId = null,
        [FromQuery] Guid? settlementId = null,
        [FromQuery] Guid? payoutId = null,
        [FromQuery] string? ownerType = null,
        [FromQuery] Guid? ownerId = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = context.JournalEntries
            .AsNoTracking()
            .Include(entry => entry.FinancialEvent)
            .Include(entry => entry.Lines)
            .AsQueryable();

        if (orderId is not null)
        {
            query = query.Where(entry => entry.FinancialEvent.OrderId == orderId || entry.Lines.Any(line => line.OrderId == orderId));
        }

        if (settlementId is not null)
        {
            query = query.Where(entry => entry.FinancialEvent.SettlementId == settlementId || entry.Lines.Any(line => line.SettlementId == settlementId));
        }

        if (payoutId is not null)
        {
            query = query.Where(entry => entry.FinancialEvent.PayoutId == payoutId || entry.Lines.Any(line => line.PayoutId == payoutId));
        }

        if (!string.IsNullOrWhiteSpace(ownerType) &&
            Enum.TryParse<FinancialOwnerType>(ownerType, true, out var parsedOwnerType))
        {
            query = query.Where(entry => entry.Lines.Any(line => line.OwnerType == parsedOwnerType));
        }

        if (ownerId is not null)
        {
            query = query.Where(entry => entry.Lines.Any(line => line.OwnerId == ownerId));
        }

        if (dateFrom is not null)
        {
            query = query.Where(entry => entry.PostedAtUtc >= dateFrom.Value);
        }

        if (dateTo is not null)
        {
            query = query.Where(entry => entry.PostedAtUtc <= dateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            var pattern = $"%{normalizedSearch}%";
            var correlationGuid = Guid.TryParse(normalizedSearch, out var parsedGuid) ? parsedGuid : (Guid?)null;

            query = query.Where(entry =>
                EF.Functions.Like(entry.Memo, pattern) ||
                EF.Functions.Like(entry.FinancialEvent.EventType.ToString(), pattern) ||
                entry.Lines.Any(line => line.Memo != null && EF.Functions.Like(line.Memo, pattern)) ||
                (correlationGuid.HasValue && entry.FinancialEvent.CorrelationId == correlationGuid.Value));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entries = await query
            .OrderByDescending(entry => entry.SequenceNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var enrichedEntries = await EnrichEntriesWithOwnerNamesAsync(entries, cancellationToken);

        return Ok(new AdminLedgerEntryListDto(
            enrichedEntries,
            page,
            pageSize,
            totalCount));
    }

    [HttpGet("ledger/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportLedger(
        [FromQuery] Guid? orderId = null,
        [FromQuery] Guid? settlementId = null,
        [FromQuery] Guid? payoutId = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.JournalEntries
            .AsNoTracking()
            .Include(entry => entry.FinancialEvent)
            .Include(entry => entry.Lines)
            .AsQueryable();

        if (orderId is not null)
        {
            query = query.Where(entry => entry.FinancialEvent.OrderId == orderId || entry.Lines.Any(line => line.OrderId == orderId));
        }

        if (settlementId is not null)
        {
            query = query.Where(entry => entry.FinancialEvent.SettlementId == settlementId || entry.Lines.Any(line => line.SettlementId == settlementId));
        }

        if (payoutId is not null)
        {
            query = query.Where(entry => entry.FinancialEvent.PayoutId == payoutId || entry.Lines.Any(line => line.PayoutId == payoutId));
        }

        var entries = await query
            .OrderByDescending(entry => entry.SequenceNumber)
            .Take(ExportLimits.MaxRows)
            .ToListAsync(cancellationToken);

        var enrichedEntries = await EnrichEntriesWithOwnerNamesAsync(entries, cancellationToken);

        var file = ExcelExportBuilder.BuildFromObjects(
            ExportFileResult.StampFileName("ledger", ".xlsx"),
            ExportText.Label("Ledger", "دفتر الأستاذ"),
            [
                ExportText.Column("ID", "المعرّف", "id"),
                ExportText.Column("Sequence", "التسلسل", "sequence"),
                ExportText.Column("Status", "الحالة", "status"),
                ExportText.Column("Event Type", "نوع الحدث", "eventType"),
                ExportText.Column("Order ID", "معرّف الطلب", "orderId"),
                ExportText.Column("Settlement ID", "معرّف التسوية", "settlementId"),
                ExportText.Column("Debit Total", "إجمالي المدين", "debitTotal"),
                ExportText.Column("Credit Total", "إجمالي الدائن", "creditTotal"),
                ExportText.Column("Posted At", "تاريخ الترحيل", "postedAt"),
                ExportText.Column("Memo", "المذكرة", "memo")
            ],
            enrichedEntries,
            entry => new Dictionary<string, string?>
            {
                ["id"] = entry.Id.ToString(),
                ["sequence"] = entry.SequenceNumber.ToString(),
                ["status"] = entry.Status.ToString(),
                ["eventType"] = entry.EventType.ToString(),
                ["orderId"] = entry.OrderId?.ToString(),
                ["settlementId"] = entry.SettlementId?.ToString(),
                ["debitTotal"] = entry.DebitTotal.ToString("0.##"),
                ["creditTotal"] = entry.CreditTotal.ToString("0.##"),
                ["postedAt"] = entry.PostedAtUtc.ToString("o"),
                ["memo"] = entry.Memo
            });

        return ExportFileResult.From(file);
    }

    [HttpGet("report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportFinanceReport(
        [FromQuery] string? title = null,
        [FromQuery] string? route = null,
        [FromQuery] string? summary = null,
        [FromQuery] string period = "month",
        CancellationToken cancellationToken = default)
    {
        var normalizedPeriod = string.IsNullOrWhiteSpace(period) ? "month" : period.Trim().ToLowerInvariant();
        var dashboard = await mediator.Send(new GetAdminFinanceDashboardQuery(normalizedPeriod), cancellationToken);
        var statement = await BuildStatementSummaryAsync(normalizedPeriod, cancellationToken);

        var kpis = new[]
        {
            dashboard.GrossCollections,
            dashboard.PlatformNetRevenue,
            dashboard.CommissionRevenue,
            dashboard.DeliveryRevenue,
            dashboard.CodFeesCollected,
            dashboard.VatCollected,
            dashboard.DriverPayouts,
            dashboard.RefundExposure
        };

        var kpiRows = kpis.Select(kpi => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
        {
            ["metric"] = ResolveKpiLabel(kpi),
            ["value"] = kpi.FormattedValue,
            ["trend"] = $"{kpi.Trend} {kpi.TrendPercent:0.##}%"
        });

        var compositionRows = dashboard.RevenueComposition.Select(segment =>
            (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
            {
                ["segment"] = ResolveCompositionLabel(segment),
                ["amount"] = segment.Amount.ToString("0.##"),
                ["percent"] = $"{segment.Percent:0.##}%"
            });

        var alertRows = dashboard.Alerts.Take(50).Select(alert =>
            (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
            {
                ["severity"] = alert.Severity,
                ["entity"] = alert.EntityName ?? alert.EntityType,
                ["amount"] = alert.Amount?.ToString("0.##") ?? string.Empty,
                ["when"] = alert.Timestamp
            });

        var tableColumns = new List<ExportColumn>
        {
            ExportText.Column("Metric", "المؤشر", "metric"),
            ExportText.Column("Value", "القيمة", "value"),
            ExportText.Column("Trend", "الاتجاه", "trend")
        };
        var tableRows = kpiRows.ToList();

        if (dashboard.RevenueComposition.Count > 0)
        {
            tableRows.Add(new Dictionary<string, string?>
            {
                ["metric"] = ExportText.Label("— Revenue composition —", "— تكوين الإيراد —"),
                ["value"] = string.Empty,
                ["trend"] = string.Empty
            });
            foreach (var row in compositionRows)
            {
                tableRows.Add(new Dictionary<string, string?>
                {
                    ["metric"] = row["segment"],
                    ["value"] = row["amount"],
                    ["trend"] = row["percent"]
                });
            }
        }

        if (dashboard.Alerts.Count > 0)
        {
            tableRows.Add(new Dictionary<string, string?>
            {
                ["metric"] = ExportText.Label("— Alerts —", "— التنبيهات —"),
                ["value"] = string.Empty,
                ["trend"] = string.Empty
            });
            foreach (var row in alertRows)
            {
                tableRows.Add(new Dictionary<string, string?>
                {
                    ["metric"] = $"{row["severity"]}: {row["entity"]}",
                    ["value"] = row["amount"],
                    ["trend"] = row["when"]
                });
            }
        }

        var file = PdfExportBuilder.BuildStatement(
            ExportFileResult.StampFileName("finance-report", ".pdf"),
            ExportText.Label("Finance Report", "التقرير المالي"),
            subtitle: ExportText.Label($"Period: {statement.PeriodLabel}", $"الفترة: {statement.PeriodLabel}"),
            meta:
            [
                ExportText.Field("Title", "العنوان", string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim()),
                ExportText.Field("Route", "المسار", string.IsNullOrWhiteSpace(route) ? string.Empty : route.Trim()),
                ExportText.Field("Summary", "الملخص", string.IsNullOrWhiteSpace(summary) ? string.Empty : summary.Trim()),
                ExportText.Field("Period", "الفترة", statement.PeriodLabel),
                ExportText.Field("Generated At (UTC)", "تاريخ الإنشاء (UTC)", DateTime.UtcNow.ToString("o"))
            ],
            columns: tableColumns,
            rows: tableRows,
            totals:
            [
                ExportText.Field("Statement Revenue", "إيراد الكشف", statement.Revenue.ToString("0.##")),
                ExportText.Field("Statement Expenses", "مصروفات الكشف", statement.Expenses.ToString("0.##")),
                ExportText.Field("VAT Payable", "ضريبة مستحقة", statement.VatPayable.ToString("0.##")),
                ExportText.Field("Net Income", "صافي الدخل", statement.NetIncome.ToString("0.##")),
                ExportText.Field("Gross Collections", "إجمالي التحصيل", dashboard.GrossCollections.FormattedValue),
                ExportText.Field("Platform Net Revenue", "صافي إيراد المنصة", dashboard.PlatformNetRevenue.FormattedValue)
            ]);

        return ExportFileResult.From(file);
    }

    private async Task<AdminFinanceStatementSummaryDto> BuildStatementSummaryAsync(
        string period,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (start, end, label) = ResolveFinancePeriod(period, now);

        var statementLines = await context.JournalLines
            .AsNoTracking()
            .Where(line =>
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.PostedAtUtc >= start &&
                line.JournalEntry.PostedAtUtc < end)
            .Where(line =>
                line.AccountCode == FinancialAccountCode.PlatformRevenue ||
                line.AccountCode == FinancialAccountCode.RefundExpense ||
                line.AccountCode == FinancialAccountCode.GatewayFeeExpense ||
                line.AccountCode == FinancialAccountCode.TaxPayable)
            .Select(line => new
            {
                line.AccountCode,
                line.DebitAmount,
                line.CreditAmount
            })
            .ToListAsync(cancellationToken);

        var revenue = statementLines
            .Where(line => line.AccountCode == FinancialAccountCode.PlatformRevenue)
            .Sum(line => line.CreditAmount);

        var expenses = statementLines
            .Where(line =>
                line.AccountCode == FinancialAccountCode.RefundExpense ||
                line.AccountCode == FinancialAccountCode.GatewayFeeExpense)
            .Sum(line => line.DebitAmount);

        var vatPayable = statementLines
            .Where(line => line.AccountCode == FinancialAccountCode.TaxPayable)
            .Sum(line => line.CreditAmount);

        return new AdminFinanceStatementSummaryDto(
            Math.Round(revenue, 2),
            Math.Round(expenses, 2),
            Math.Round(vatPayable, 2),
            Math.Round(revenue - expenses - vatPayable, 2),
            label);
    }

    private static (DateTime Start, DateTime End, string Label) ResolveFinancePeriod(string period, DateTime now)
    {
        switch (period)
        {
            case "today":
            {
                var start = now.Date;
                return (start, start.AddDays(1), start.ToString("yyyy-MM-dd"));
            }
            case "week":
            {
                var start = now.Date.AddDays(-7);
                return (start, now, $"{start:yyyy-MM-dd}..{now:yyyy-MM-dd}");
            }
            case "quarter":
            {
                var start = now.Date.AddDays(-90);
                return (start, now, $"{start:yyyy-MM-dd}..{now:yyyy-MM-dd}");
            }
            default:
            {
                var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                return (start, start.AddMonths(1), $"{start:yyyy-MM}");
            }
        }
    }

    private static string ResolveKpiLabel(AdminFinanceKpiDto kpi) =>
        string.IsNullOrWhiteSpace(kpi.Id)
            ? kpi.LabelKey
            : kpi.Id switch
            {
                "gross_collections" => ExportText.Label("Gross Collections", "إجمالي التحصيل"),
                "platform_net_revenue" => ExportText.Label("Platform Net Revenue", "صافي إيراد المنصة"),
                "commission_revenue" => ExportText.Label("Commission Revenue", "إيراد العمولة"),
                "delivery_revenue" => ExportText.Label("Delivery Revenue", "إيراد التوصيل"),
                "cod_fees" => ExportText.Label("COD Fees Collected", "رسوم الدفع عند الاستلام"),
                "vat_collected" => ExportText.Label("VAT Collected", "ضريبة محصّلة"),
                "driver_payouts" => ExportText.Label("Driver Payouts", "مدفوعات المناديب"),
                "refund_exposure" => ExportText.Label("Refund Exposure", "تعرّض الاسترداد"),
                _ => kpi.Id.Replace('_', ' ')
            };

    private static string ResolveCompositionLabel(AdminRevenueCompositionSegmentDto segment) =>
        string.IsNullOrWhiteSpace(segment.Id)
            ? segment.LabelKey
            : segment.Id switch
            {
                "commissions" => ExportText.Label("Commissions", "العمولات"),
                "delivery_fees" => ExportText.Label("Delivery Fees", "رسوم التوصيل"),
                "cod_fees" => ExportText.Label("COD Fees", "رسوم الدفع عند الاستلام"),
                "vat" => ExportText.Label("VAT", "الضريبة"),
                _ => segment.Id.Replace('_', ' ')
            };

    [HttpGet("ledger/{entryId:guid}")]
    [ProducesResponseType(typeof(AdminLedgerEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminLedgerEntryDto>> GetLedgerEntry(
        [FromRoute] Guid entryId,
        CancellationToken cancellationToken = default)
    {
        var entry = await context.JournalEntries
            .AsNoTracking()
            .Include(item => item.FinancialEvent)
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.Id == entryId, cancellationToken);

        if (entry is null)
        {
            return NotFound();
        }

        var enrichedEntries = await EnrichEntriesWithOwnerNamesAsync([entry], cancellationToken);
        return Ok(enrichedEntries.First());
    }

    [HttpGet("audit-log/stats")]
    [ProducesResponseType(typeof(AdminFinanceAuditLogStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminFinanceAuditLogStatsDto>> GetAuditLogStats(
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? orderId = null,
        [FromQuery] string? actionCategory = null,
        CancellationToken cancellationToken = default)
    {
        var filteredEntries = await BuildFilteredAuditEntriesAsync(
            entityType,
            entityId,
            orderId,
            actionCategory,
            sourceFetchLimit: 5000,
            cancellationToken);

        var systemEntries = filteredEntries.Count(entry =>
            entry.AdminId == "finance-system" ||
            string.Equals(entry.AdminName, "FINANCES.AUDIT.ADMINS.FINANCE_SYSTEM", StringComparison.Ordinal));

        var affectedEntities = filteredEntries
            .Select(entry => $"{entry.EntityType}:{entry.EntityId ?? entry.OrderId ?? entry.Id}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return Ok(new AdminFinanceAuditLogStatsDto(
            filteredEntries.Count,
            systemEntries,
            Math.Max(filteredEntries.Count - systemEntries, 0),
            affectedEntities));
    }

    [HttpGet("audit-log")]
    [ProducesResponseType(typeof(AdminFinanceAuditLogListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminFinanceAuditLogListDto>> GetAuditLog(
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? orderId = null,
        [FromQuery] string? actionCategory = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var filteredEntries = await BuildFilteredAuditEntriesAsync(
            entityType,
            entityId,
            orderId,
            actionCategory,
            sourceFetchLimit: pageSize,
            cancellationToken);

        var entries = filteredEntries
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new AdminFinanceAuditLogListDto(entries, filteredEntries.Count));
    }

    [HttpPost("cod-remittances")]
    [ProducesResponseType(typeof(AdminCodRemittanceResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminCodRemittanceResultDto>> CreateCodRemittance(
        [FromBody] CreateCodRemittanceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.DriverId == Guid.Empty)
        {
            return BadRequest(ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_ID_REQUIRED"));
        }

        if (request.Amount <= 0)
        {
            return BadRequest(ApiLocalizedMessages.Resolve(HttpContext, "AMOUNT_GREATER_THAN_ZERO"));
        }

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"cod-remittance:{request.DriverId:N}:{DateTime.UtcNow:yyyyMMddHHmmssfffffff}"
            : request.IdempotencyKey.Trim();

        var result = await financialEventPostingService.PostAsync(
            FinancialEventType.DriverCashRemittance,
            idempotencyKey,
            [
                new JournalLineDraft(
                    FinancialAccountCode.PlatformCash,
                    request.Amount,
                    0m,
                    FinancialOwnerType.Platform,
                    request.PlatformOwnerId,
                    Memo: request.Reference ?? $"COD remittance from driver {request.DriverId}"),
                new JournalLineDraft(
                    FinancialAccountCode.DriverCodReceivable,
                    0m,
                    request.Amount,
                    FinancialOwnerType.Driver,
                    request.DriverId,
                    Memo: request.Reference ?? $"COD remittance from driver {request.DriverId}")
            ],
            description: request.Reference ?? $"COD remittance from driver {request.DriverId}",
            cancellationToken: cancellationToken);

        await walletProjectionUpdater.ApplyJournalEntryAsync(result.JournalEntryId, cancellationToken);

        return Ok(new AdminCodRemittanceResultDto(
            result.FinancialEventId,
            result.JournalEntryId,
            result.SequenceNumber,
            result.WasAlreadyPosted));
    }

    [HttpPost("vendor-cod-remittances")]
    [ProducesResponseType(typeof(AdminCodRemittanceResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminCodRemittanceResultDto>> CreateVendorCodRemittance(
        [FromBody] CreateVendorCodRemittanceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.VendorId == Guid.Empty)
        {
            return BadRequest(ApiLocalizedMessages.Resolve(HttpContext, "VENDOR_ID_REQUIRED"));
        }

        if (request.Amount <= 0)
        {
            return BadRequest(ApiLocalizedMessages.Resolve(HttpContext, "AMOUNT_GREATER_THAN_ZERO"));
        }

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"vendor-cod-remittance:{request.VendorId:N}:{DateTime.UtcNow:yyyyMMddHHmmssfffffff}"
            : request.IdempotencyKey.Trim();

        var result = await financialEventPostingService.PostAsync(
            FinancialEventType.VendorCashRemittance,
            idempotencyKey,
            [
                new JournalLineDraft(
                    FinancialAccountCode.PlatformCash,
                    request.Amount,
                    0m,
                    FinancialOwnerType.Platform,
                    request.PlatformOwnerId,
                    Memo: request.Reference ?? $"Cash-on-pickup remittance from vendor {request.VendorId}"),
                new JournalLineDraft(
                    FinancialAccountCode.VendorCodReceivable,
                    0m,
                    request.Amount,
                    FinancialOwnerType.Vendor,
                    request.VendorId,
                    Memo: request.Reference ?? $"Cash-on-pickup remittance from vendor {request.VendorId}")
            ],
            description: request.Reference ?? $"Cash-on-pickup remittance from vendor {request.VendorId}",
            cancellationToken: cancellationToken);

        await walletProjectionUpdater.ApplyJournalEntryAsync(result.JournalEntryId, cancellationToken);

        return Ok(new AdminCodRemittanceResultDto(
            result.FinancialEventId,
            result.JournalEntryId,
            result.SequenceNumber,
            result.WasAlreadyPosted));
    }

    [HttpGet("vendor-cod-reconciliation")]
    [ProducesResponseType(typeof(AdminVendorCodReconciliationListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminVendorCodReconciliationListDto>> GetVendorCodReconciliation(
        CancellationToken cancellationToken)
    {
        var vendorWallets = await context.Wallets
            .AsNoTracking()
            .Where(wallet => wallet.OwnerType == Zadana.Domain.Modules.Wallets.Enums.WalletOwnerType.Vendor &&
                             wallet.CodOwedBalance != 0)
            .OrderByDescending(wallet => wallet.CodOwedBalance)
            .ToListAsync(cancellationToken);

        var vendorIds = vendorWallets.Select(wallet => wallet.OwnerId).ToList();
        var vendors = await context.Vendors
            .AsNoTracking()
            .Where(vendor => vendorIds.Contains(vendor.Id))
            .ToDictionaryAsync(vendor => vendor.Id, cancellationToken);

        var items = vendorWallets.Select(wallet =>
        {
            vendors.TryGetValue(wallet.OwnerId, out var vendor);
            return new AdminVendorCodReconciliationDto(
                wallet.OwnerId,
                vendor?.BusinessNameAr ?? vendor?.BusinessNameEn ?? "Unknown vendor",
                wallet.CodOwedBalance,
                wallet.LastJournalSequence);
        }).ToList();

        return Ok(new AdminVendorCodReconciliationListDto(items, items.Sum(item => item.CodOwedBalance)));
    }

    [HttpGet("cod-reconciliation")]
    [ProducesResponseType(typeof(AdminCodReconciliationListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminCodReconciliationListDto>> GetCodReconciliation(CancellationToken cancellationToken)
    {
        var driverWallets = await context.Wallets
            .AsNoTracking()
            .Where(wallet => wallet.OwnerType == Zadana.Domain.Modules.Wallets.Enums.WalletOwnerType.Driver && wallet.CodOwedBalance != 0)
            .OrderByDescending(wallet => wallet.CodOwedBalance)
            .ToListAsync(cancellationToken);

        var driverIds = driverWallets.Select(wallet => wallet.OwnerId).ToList();
        var drivers = await context.Drivers
            .AsNoTracking()
            .Include(driver => driver.User)
            .Where(driver => driverIds.Contains(driver.Id))
            .ToDictionaryAsync(driver => driver.Id, cancellationToken);

        var items = driverWallets.Select(wallet =>
        {
            drivers.TryGetValue(wallet.OwnerId, out var driver);
            return new AdminCodReconciliationDto(
                wallet.OwnerId,
                driver?.User.FullName ?? "Unknown driver",
                driver?.User.PhoneNumber ?? string.Empty,
                wallet.CodOwedBalance,
                wallet.LastJournalSequence);
        }).ToList();

        return Ok(new AdminCodReconciliationListDto(items, items.Sum(item => item.CodOwedBalance)));
    }

    [HttpGet("cod-reconciliation/{driverId:guid}")]
    [ProducesResponseType(typeof(AdminCodDriverReconciliationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminCodDriverReconciliationDto>> GetDriverCodReconciliation(
        [FromRoute] Guid driverId,
        CancellationToken cancellationToken)
    {
        var wallet = await context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.OwnerType == Zadana.Domain.Modules.Wallets.Enums.WalletOwnerType.Driver &&
                item.OwnerId == driverId,
                cancellationToken);

        var lines = await context.JournalLines
            .AsNoTracking()
            .Where(line =>
                line.AccountCode == FinancialAccountCode.DriverCodReceivable &&
                line.OwnerType == FinancialOwnerType.Driver &&
                line.OwnerId == driverId)
            .OrderByDescending(line => line.CreatedAtUtc)
            .Take(200)
            .Select(line => new AdminCodDriverLineDto(
                line.Id,
                line.OrderId,
                line.DebitAmount,
                line.CreditAmount,
                line.Memo,
                line.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new AdminCodDriverReconciliationDto(
            driverId,
            wallet?.CodOwedBalance ?? 0m,
            lines));
    }

    [HttpPost("rebuild-wallet-projections")]
    [ProducesResponseType(typeof(WalletProjectionRebuildResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<WalletProjectionRebuildResult>> RebuildWalletProjections(CancellationToken cancellationToken)
    {
        return Ok(await walletProjectionUpdater.RebuildAllAsync(cancellationToken));
    }

    [HttpGet("reconciliation-report")]
    [ProducesResponseType(typeof(WalletProjectionReconciliationReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<WalletProjectionReconciliationReport>> GetReconciliationReport(CancellationToken cancellationToken)
    {
        return Ok(await walletProjectionUpdater.BuildReconciliationReportAsync(cancellationToken));
    }

    [HttpGet("revenue-reconciliation/preview")]
    [ProducesResponseType(typeof(RevenueReconciliationPreviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RevenueReconciliationPreviewDto>> PreviewRevenueReconciliation(
        [FromQuery] int maxOrders = 500,
        CancellationToken cancellationToken = default)
    {
        return Ok(await revenueReconciliationService.PreviewAsync(maxOrders, cancellationToken));
    }

    [HttpPost("revenue-reconciliation/apply")]
    [ProducesResponseType(typeof(RevenueReconciliationApplyResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RevenueReconciliationApplyResultDto>> ApplyRevenueReconciliation(
        [FromBody] RevenueReconciliationApplyRequest? request,
        CancellationToken cancellationToken = default)
    {
        return Ok(await revenueReconciliationService.ApplyAsync(request?.MaxOrders ?? 500, cancellationToken));
    }

    private async Task<List<AdminLedgerEntryDto>> EnrichEntriesWithOwnerNamesAsync(
        IEnumerable<Zadana.Domain.Modules.Finances.Entities.JournalEntry> entries,
        CancellationToken cancellationToken)
    {
        var allVendorIds = entries
            .SelectMany(e => e.Lines)
            .Where(line => line.OwnerType == FinancialOwnerType.Vendor && line.OwnerId.HasValue)
            .Select(line => line.OwnerId!.Value)
            .Distinct()
            .ToList();

        var allDriverIds = entries
            .SelectMany(e => e.Lines)
            .Where(line => line.OwnerType == FinancialOwnerType.Driver && line.OwnerId.HasValue)
            .Select(line => line.OwnerId!.Value)
            .Distinct()
            .ToList();

        var vendorNames = await financeOwnerNameResolver.BatchResolveVendorNamesAsync(allVendorIds, cancellationToken);
        var driverNames = await financeOwnerNameResolver.BatchResolveDriverNamesAsync(allDriverIds, cancellationToken);

        return entries.Select(entry => ToDto(entry, vendorNames, driverNames)).ToList();
    }

    private static AdminLedgerEntryDto ToDto(
        Zadana.Domain.Modules.Finances.Entities.JournalEntry entry,
        Dictionary<Guid, string> vendorNames,
        Dictionary<Guid, string> driverNames)
    {
        var lines = entry.Lines
            .OrderBy(line => line.CreatedAtUtc)
            .ThenBy(line => line.Id)
            .Select(line =>
            {
                string? ownerName = null;
                if (line.OwnerType == FinancialOwnerType.Vendor && line.OwnerId.HasValue)
                {
                    ownerName = vendorNames.GetValueOrDefault(line.OwnerId.Value);
                }
                else if (line.OwnerType == FinancialOwnerType.Driver && line.OwnerId.HasValue)
                {
                    ownerName = driverNames.GetValueOrDefault(line.OwnerId.Value);
                }
                else if (line.OwnerType == FinancialOwnerType.Platform)
                {
                    ownerName = "Platform";
                }

                return new AdminLedgerLineDto(
                    line.Id,
                    line.AccountCode,
                    line.OwnerType,
                    line.OwnerId,
                    ownerName,
                    line.DebitAmount,
                    line.CreditAmount,
                    line.CurrencyCode,
                    line.OrderId,
                    line.SettlementId,
                    line.PayoutId,
                    line.Memo);
            })
            .ToList();

        return new AdminLedgerEntryDto(
            entry.Id,
            entry.SequenceNumber,
            entry.Status,
            entry.FinancialEvent.EventType,
            entry.FinancialEvent.CorrelationId,
            entry.FinancialEvent.IdempotencyKey,
            entry.FinancialEvent.OrderId,
            entry.FinancialEvent.SettlementId,
            entry.FinancialEvent.PayoutId,
            entry.FinancialEvent.RefundId,
            entry.CurrencyCode,
            entry.PostedAtUtc,
            lines.Sum(line => line.DebitAmount),
            lines.Sum(line => line.CreditAmount),
            entry.Memo,
            lines);
    }

    private static AdminFinanceAuditLogEntryDto ToAuditDto(Zadana.Domain.Modules.Finances.Entities.JournalEntry entry)
    {
        var line = entry.Lines
            .OrderByDescending(item => item.OwnerType.HasValue && item.OwnerType.Value != FinancialOwnerType.Platform)
            .ThenByDescending(item => item.DebitAmount + item.CreditAmount)
            .FirstOrDefault();

        var eventType = entry.FinancialEvent.EventType.ToString();
        var accountCode = line?.AccountCode.ToString();

        return new AdminFinanceAuditLogEntryDto(
            $"audit-ledger-{entry.Id:N}",
            entry.PostedAtUtc,
            "finance-system",
            "FINANCES.AUDIT.ADMINS.FINANCE_SYSTEM",
            "FINANCES.AUDIT.ROLES.SYSTEM",
            "FINANCES.AUDIT.ACTIONS.LEDGER_POSTED",
            ToAuditCategory(entry.FinancialEvent.EventType, line?.AccountCode),
            ToAuditEntityType(line?.OwnerType),
            line?.OwnerId?.ToString(),
            (line?.OrderId ?? entry.FinancialEvent.OrderId)?.ToString(),
            BuildAuditEntityName(line?.OwnerType, line?.OwnerId),
            null,
            new Dictionary<string, object?>
            {
                ["sequenceNumber"] = entry.SequenceNumber,
                ["status"] = entry.Status.ToString(),
                ["eventType"] = eventType,
                ["accountCode"] = accountCode,
                ["debitAmount"] = line?.DebitAmount,
                ["creditAmount"] = line?.CreditAmount,
                ["currency"] = entry.CurrencyCode,
                ["correlationId"] = entry.FinancialEvent.CorrelationId,
                ["memo"] = line?.Memo ?? entry.Memo ?? entry.FinancialEvent.Description
            },
            null,
            null);
    }

    private static AdminFinanceAuditLogEntryDto ToAuditDto(Zadana.Domain.Modules.Wallets.Entities.Settlement settlement)
    {
        return new AdminFinanceAuditLogEntryDto(
            $"audit-settlement-{settlement.Id:N}",
            settlement.ProcessedAtUtc ?? settlement.PeriodTo,
            "finance-system",
            "FINANCES.AUDIT.ADMINS.FINANCE_SYSTEM",
            "FINANCES.AUDIT.ROLES.SYSTEM",
            ToSettlementAuditAction(settlement.Status),
            "settlement",
            ToAuditEntityType(settlement.OwnerType),
            settlement.OwnerId.ToString(),
            null,
            BuildAuditEntityName(settlement.OwnerType, settlement.OwnerId),
            null,
            new Dictionary<string, object?>
            {
                ["status"] = settlement.Status.ToString(),
                ["resolutionType"] = settlement.ResolutionType.ToString(),
                ["origin"] = settlement.Origin.ToString(),
                ["periodFrom"] = settlement.PeriodFrom,
                ["periodTo"] = settlement.PeriodTo,
                ["grossAmount"] = settlement.GrossAmount,
                ["commissionAmount"] = settlement.CommissionAmount,
                ["refundAmount"] = settlement.RefundAmount,
                ["adjustmentAmount"] = settlement.AdjustmentAmount,
                ["recoveryAmount"] = settlement.RecoveryAmount,
                ["netAmount"] = settlement.NetAmount
            },
            null,
            null);
    }

    private static AdminFinanceAuditLogEntryDto ToCodAuditDto(
        Guid driverId,
        string? driverName,
        decimal codOwedBalance,
        long lastJournalSequence,
        Guid? orderId,
        DateTime? lastActivityUtc,
        string? memo)
    {
        return new AdminFinanceAuditLogEntryDto(
            $"audit-cod-{driverId:N}",
            lastActivityUtc ?? DateTime.UtcNow,
            "finance-system",
            "FINANCES.AUDIT.ADMINS.FINANCE_SYSTEM",
            "FINANCES.AUDIT.ROLES.SYSTEM",
            codOwedBalance > 0 ? "FINANCES.AUDIT.ACTIONS.COD_OVERDUE" : "FINANCES.AUDIT.ACTIONS.COD_RECONCILED",
            "override",
            "driver",
            driverId.ToString(),
            orderId?.ToString(),
            driverName ?? BuildAuditEntityName(FinancialOwnerType.Driver, driverId),
            null,
            new Dictionary<string, object?>
            {
                ["codOwedBalance"] = codOwedBalance,
                ["lastJournalSequence"] = lastJournalSequence,
                ["memo"] = memo
            },
            null,
            null);
    }

    private static bool MatchesAuditFilter(
        AdminFinanceAuditLogEntryDto entry,
        string? entityType,
        string? entityId,
        string? orderId,
        string? actionCategory)
    {
        if (!string.IsNullOrWhiteSpace(entityType) && !string.Equals(entry.EntityType, entityType, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(entityId) && !string.Equals(entry.EntityId, entityId, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(orderId) && !string.Equals(entry.OrderId, orderId, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(actionCategory) && !string.Equals(entry.ActionCategory, actionCategory, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static string ToAuditCategory(FinancialEventType eventType, FinancialAccountCode? accountCode)
    {
        var name = eventType.ToString();
        if (name.Contains("Refund", StringComparison.OrdinalIgnoreCase)) return "refund";
        if (name.Contains("Payout", StringComparison.OrdinalIgnoreCase) || name.Contains("Settlement", StringComparison.OrdinalIgnoreCase)) return "settlement";
        if (name.Contains("Adjustment", StringComparison.OrdinalIgnoreCase) || accountCode == FinancialAccountCode.ManualAdjustment) return "adjustment";
        return "override";
    }

    private static string ToSettlementAuditAction(SettlementStatus status) =>
        status switch
        {
            SettlementStatus.PaidOut or SettlementStatus.Settled => "FINANCES.AUDIT.ACTIONS.SETTLEMENT_PAID",
            SettlementStatus.Approved or SettlementStatus.Processing => "FINANCES.AUDIT.ACTIONS.SETTLEMENT_APPROVED",
            _ => "FINANCES.AUDIT.ACTIONS.SETTLEMENT_CREATED"
        };

    private static string ToAuditEntityType(FinancialOwnerType? ownerType) =>
        ownerType switch
        {
            FinancialOwnerType.Vendor => "vendor",
            FinancialOwnerType.Driver => "driver",
            FinancialOwnerType.Customer => "customer",
            _ => "platform"
        };

    private static string ToAuditEntityType(SettlementOwnerType ownerType) =>
        ownerType switch
        {
            SettlementOwnerType.Vendor => "vendor",
            SettlementOwnerType.Driver => "driver",
            _ => "platform"
        };

    private static string BuildAuditEntityName(FinancialOwnerType? ownerType, Guid? ownerId) =>
        ownerType.HasValue && ownerId.HasValue
            ? $"{ownerType.Value} {ownerId.Value.ToString("N")[..8].ToUpperInvariant()}"
            : "Platform";

    private static string BuildAuditEntityName(SettlementOwnerType ownerType, Guid ownerId) =>
        $"{ownerType} {ownerId.ToString("N")[..8].ToUpperInvariant()}";

    private async Task<List<AdminFinanceAuditLogEntryDto>> BuildFilteredAuditEntriesAsync(
        string? entityType,
        string? entityId,
        string? orderId,
        string? actionCategory,
        int sourceFetchLimit,
        CancellationToken cancellationToken)
    {
        sourceFetchLimit = Math.Clamp(sourceFetchLimit, 1, 5000);

        var journalEntries = await context.JournalEntries
            .AsNoTracking()
            .Include(entry => entry.FinancialEvent)
            .Include(entry => entry.Lines)
            .OrderByDescending(entry => entry.PostedAtUtc)
            .Take(sourceFetchLimit)
            .ToListAsync(cancellationToken);

        var settlements = await context.Settlements
            .AsNoTracking()
            .OrderByDescending(settlement => settlement.ProcessedAtUtc ?? settlement.PeriodTo)
            .Take(sourceFetchLimit)
            .ToListAsync(cancellationToken);

        var codWallets = await context.Wallets
            .AsNoTracking()
            .Where(wallet =>
                wallet.OwnerType == WalletOwnerType.Driver &&
                wallet.CodOwedBalance != 0)
            .OrderByDescending(wallet => wallet.CodOwedBalance)
            .Take(sourceFetchLimit)
            .ToListAsync(cancellationToken);

        var codDriverIds = codWallets.Select(wallet => wallet.OwnerId).ToList();
        var codDrivers = codDriverIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.Drivers
                .AsNoTracking()
                .Include(driver => driver.User)
                .Where(driver => codDriverIds.Contains(driver.Id))
                .ToDictionaryAsync(driver => driver.Id, driver => driver.User.FullName, cancellationToken);

        var latestCodLines = codDriverIds.Count == 0
            ? []
            : await context.JournalLines
                .AsNoTracking()
                .Where(line =>
                    line.AccountCode == FinancialAccountCode.DriverCodReceivable &&
                    line.OwnerType == FinancialOwnerType.Driver &&
                    line.OwnerId.HasValue &&
                    codDriverIds.Contains(line.OwnerId.Value))
                .OrderByDescending(line => line.CreatedAtUtc)
                .Select(line => new
                {
                    line.OwnerId,
                    line.OrderId,
                    line.DebitAmount,
                    line.CreditAmount,
                    line.Memo,
                    line.CreatedAtUtc
                })
                .ToListAsync(cancellationToken);

        var latestCodLineByDriver = latestCodLines
            .Where(line => line.OwnerId.HasValue)
            .GroupBy(line => line.OwnerId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        return journalEntries
            .Select(ToAuditDto)
            .Concat(settlements.Select(ToAuditDto))
            .Concat(codWallets.Select(wallet =>
            {
                latestCodLineByDriver.TryGetValue(wallet.OwnerId, out var latestLine);
                codDrivers.TryGetValue(wallet.OwnerId, out var driverName);
                return ToCodAuditDto(
                    wallet.OwnerId,
                    driverName,
                    wallet.CodOwedBalance,
                    wallet.LastJournalSequence,
                    latestLine?.OrderId,
                    latestLine?.CreatedAtUtc,
                    latestLine?.Memo);
            }))
            .Where(entry => MatchesAuditFilter(entry, entityType, entityId, orderId, actionCategory))
            .OrderByDescending(entry => entry.TimestampUtc)
            .ToList();
    }

}

public sealed record AdminFinanceAuditLogListDto(
    IReadOnlyList<AdminFinanceAuditLogEntryDto> Items,
    int TotalCount);

public sealed record AdminFinanceAuditLogStatsDto(
    int TotalEntries,
    int SystemEntries,
    int ManualActions,
    int AffectedEntities);

public sealed record AdminFinanceAuditLogEntryDto(
    string Id,
    DateTime TimestampUtc,
    string AdminId,
    string AdminName,
    string AdminRole,
    string Action,
    string ActionCategory,
    string EntityType,
    string? EntityId,
    string? OrderId,
    string? EntityName,
    IReadOnlyDictionary<string, object?>? Before,
    IReadOnlyDictionary<string, object?>? After,
    string? IpAddress,
    string? SessionId);

public sealed record CreateCodRemittanceRequest(
    Guid DriverId,
    decimal Amount,
    string? Reference,
    string? IdempotencyKey,
    Guid? PlatformOwnerId);

public sealed record CreateVendorCodRemittanceRequest(
    Guid VendorId,
    decimal Amount,
    string? Reference,
    string? IdempotencyKey,
    Guid? PlatformOwnerId);

public sealed record UpdateZoneFinanceSettingsRequest(
    decimal VatPercent,
    string CodFeeType,
    decimal CodFlatFee,
    decimal CodPercent,
    bool IsVatActive,
    bool IsCodFeeActive);

public sealed record UpdateDeliveryPricingRequest(
    decimal BaseDeliveryFee,
    decimal IncludedKm,
    decimal ExtraKmFee,
    decimal MinDeliveryFee,
    decimal MaxDeliveryFee,
    bool IsPricingActive,
    decimal VatPercent,
    string CodFeeType,
    decimal CodFlatFee,
    decimal CodPercent,
    bool IsVatActive,
    bool IsCodFeeActive);

public sealed record UpdateDeliveryPricingDefaultsRequest(
    Guid Id,
    decimal BaseDeliveryFee,
    decimal IncludedKm,
    decimal ExtraKmFee,
    decimal MinDeliveryFee,
    decimal MaxDeliveryFee,
    bool IsPricingActive,
    decimal VatPercent,
    string CodFeeType,
    decimal CodFlatFee,
    decimal CodPercent,
    bool IsVatActive,
    bool IsCodFeeActive,
    decimal MinTotalDeliveryFee,
    decimal MaxTotalDeliveryFee,
    decimal MaxQuotedDistanceKm,
    decimal WarningSubtotalRatioThreshold);

public sealed record AdminVendorCodReconciliationDto(
    Guid VendorId,
    string VendorName,
    decimal CodOwedBalance,
    long LastJournalSequence);

public sealed record AdminVendorCodReconciliationListDto(
    IReadOnlyList<AdminVendorCodReconciliationDto> Items,
    decimal TotalCodOwedBalance);

public sealed record AdminCodRemittanceResultDto(
    Guid FinancialEventId,
    Guid JournalEntryId,
    long SequenceNumber,
    bool WasAlreadyPosted);

public sealed record AdminCodReconciliationListDto(
    IReadOnlyList<AdminCodReconciliationDto> Items,
    decimal TotalCodOwed);

public sealed record AdminCodReconciliationDto(
    Guid DriverId,
    string DriverName,
    string DriverPhone,
    decimal CodOwedBalance,
    long LastJournalSequence);

public sealed record AdminCodDriverReconciliationDto(
    Guid DriverId,
    decimal CodOwedBalance,
    IReadOnlyList<AdminCodDriverLineDto> Lines);

public sealed record AdminCodDriverLineDto(
    Guid JournalLineId,
    Guid? OrderId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Memo,
    DateTime CreatedAtUtc);
