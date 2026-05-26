using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        if (zoneId != command.ZoneId) return BadRequest("ZoneId mismatch");

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
        if (cityId != command.CityId) return BadRequest("CityId mismatch");

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
        if (regionId != command.RegionId) return BadRequest("RegionId mismatch");

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

    [HttpPost("cod-remittances")]
    [ProducesResponseType(typeof(AdminCodRemittanceResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminCodRemittanceResultDto>> CreateCodRemittance(
        [FromBody] CreateCodRemittanceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.DriverId == Guid.Empty)
        {
            return BadRequest("DriverId is required.");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("Amount must be greater than zero.");
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

}

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
