using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Localization;
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
    RevenueReconciliationService revenueReconciliationService) : ControllerBase
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

        var vendorCommission = order.VendorCommissionAmount > 0
            ? order.VendorCommissionAmount
            : order.CommissionAmount;
        var driverCommission = order.DriverCommissionAmount;
        var driverPayout = Math.Max(0m, order.DeliveryFee - driverCommission);
        var productNet = order.ProductNet > 0 ? order.ProductNet : Math.Max(0m, order.Subtotal - order.DiscountTotal);
        var vendorEarnings = Math.Max(0m, productNet - vendorCommission);
        var platformRevenue = Math.Round(vendorCommission + driverCommission + order.CodFee, 2);
        var total = order.TotalAmount;
        var netMargin = Math.Round(platformRevenue - order.VatAmount, 2);
        var marginPercent = total > 0 ? Math.Round((netMargin / total) * 100m, 2) : 0m;

        return Ok(new AdminOrderFinancialBreakdownDto(
            order.Id,
            order.OrderNumber,
            order.Subtotal,
            order.DiscountTotal,
            order.DiscountTotal,
            order.DeliveryFee,
            0m,
            order.CodFee,
            order.VatAmount,
            total,
            vendorEarnings,
            vendorCommission,
            driverPayout,
            platformRevenue,
            netMargin,
            marginPercent));
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
        [FromBody] UpdateZoneFinanceSettingsCommand command,
        CancellationToken cancellationToken)
    {
        if (zoneId != command.ZoneId) return BadRequest(ApiLocalizedMessages.Resolve(HttpContext, "ZONE_ID_MISMATCH"));

        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPut("city-pricing/{cityId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CityDeliveryPricingSettingsDto>> UpdateCityPricing(
        [FromRoute] Guid cityId,
        [FromBody] UpdateCityDeliveryPricingSettingsCommand command,
        CancellationToken cancellationToken)
    {
        if (cityId != command.CityId) return BadRequest(ApiLocalizedMessages.Resolve(HttpContext, "CITY_ID_MISMATCH"));

        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPut("region-pricing/{regionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RegionDeliveryPricingSettingsDto>> UpdateRegionPricing(
        [FromRoute] Guid regionId,
        [FromBody] UpdateRegionDeliveryPricingSettingsCommand command,
        CancellationToken cancellationToken)
    {
        if (regionId != command.RegionId) return BadRequest(ApiLocalizedMessages.Resolve(HttpContext, "REGION_ID_MISMATCH"));

        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPut("delivery-defaults")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeliveryPricingDefaultsDto>> UpdateDeliveryDefaults(
        [FromBody] UpdateDeliveryPricingDefaultsCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("ledger")]
    [ProducesResponseType(typeof(AdminLedgerEntryListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminLedgerEntryListDto>> GetLedger(
        [FromQuery] Guid? orderId = null,
        [FromQuery] Guid? settlementId = null,
        [FromQuery] Guid? payoutId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

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

        var totalCount = await query.CountAsync(cancellationToken);
        var entries = await query
            .OrderByDescending(entry => entry.SequenceNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new AdminLedgerEntryListDto(
            entries.Select(ToDto).ToList(),
            page,
            pageSize,
            totalCount));
    }

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

        return Ok(ToDto(entry));
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

        var journalEntries = await context.JournalEntries
            .AsNoTracking()
            .Include(entry => entry.FinancialEvent)
            .Include(entry => entry.Lines)
            .OrderByDescending(entry => entry.PostedAtUtc)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var settlements = await context.Settlements
            .AsNoTracking()
            .OrderByDescending(settlement => settlement.ProcessedAtUtc ?? settlement.PeriodTo)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var codWallets = await context.Wallets
            .AsNoTracking()
            .Where(wallet =>
                wallet.OwnerType == Zadana.Domain.Modules.Wallets.Enums.WalletOwnerType.Driver &&
                wallet.CodOwedBalance != 0)
            .OrderByDescending(wallet => wallet.CodOwedBalance)
            .Take(pageSize)
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

        var filteredEntries = journalEntries
            .Select(ToAuditDto)
            .Concat(settlements.Select(ToAuditDto))
            .Concat(codWallets.Select(wallet =>
            {
                latestCodLineByDriver.TryGetValue(wallet.OwnerId, out var latestLine);
                codDrivers.TryGetValue(wallet.OwnerId, out var driverName);
                return ToCodAuditDto(wallet.OwnerId, driverName, wallet.CodOwedBalance, wallet.LastJournalSequence, latestLine?.OrderId, latestLine?.CreatedAtUtc, latestLine?.Memo);
            }))
            .Where(entry => MatchesAuditFilter(entry, entityType, entityId, orderId, actionCategory))
            .OrderByDescending(entry => entry.TimestampUtc)
            .ToList();

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

    private static AdminLedgerEntryDto ToDto(Zadana.Domain.Modules.Finances.Entities.JournalEntry entry)
    {
        var lines = entry.Lines
            .OrderBy(line => line.CreatedAtUtc)
            .ThenBy(line => line.Id)
            .Select(line => new AdminLedgerLineDto(
                line.Id,
                line.AccountCode,
                line.OwnerType,
                line.OwnerId,
                line.DebitAmount,
                line.CreditAmount,
                line.CurrencyCode,
                line.OrderId,
                line.SettlementId,
                line.PayoutId,
                line.Memo))
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

}

public sealed record AdminFinanceAuditLogListDto(
    IReadOnlyList<AdminFinanceAuditLogEntryDto> Items,
    int TotalCount);

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
