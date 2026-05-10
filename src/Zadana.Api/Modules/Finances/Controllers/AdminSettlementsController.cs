using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Api.Modules.Finances.Controllers;

[ApiController]
[Route("api/admin/settlements")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminSettlementsController(IApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminSettlementListDto>> GetSettlements(
        [FromQuery] string? ownerType = null,
        [FromQuery] Guid? ownerId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = context.Settlements.AsNoTracking().AsQueryable();
        if (Enum.TryParse<SettlementOwnerType>(ownerType, true, out var parsedOwnerType))
        {
            query = query.Where(item => item.OwnerType == parsedOwnerType);
        }

        if (ownerId.HasValue)
        {
            query = query.Where(item => item.OwnerId == ownerId.Value);
        }

        if (Enum.TryParse<SettlementStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(item => item.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => ToDto(item))
            .ToListAsync(cancellationToken);

        return Ok(new AdminSettlementListDto(items, page, pageSize, totalCount));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminSettlementDetailDto>> GetSettlement(Guid id, CancellationToken cancellationToken)
    {
        var settlement = await context.Settlements
            .AsNoTracking()
            .Include(item => item.Items)
            .Include(item => item.Payouts)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (settlement is null)
        {
            return NotFound();
        }

        return Ok(new AdminSettlementDetailDto(
            ToDto(settlement),
            settlement.Items.Select(item => new AdminSettlementItemDto(
                item.Id,
                item.LineType.ToString(),
                item.SourceId,
                item.OrderId,
                item.Amount,
                item.Commission,
                item.Refund,
                item.Adjustment,
                item.Recovery,
                item.NetAmount)).ToList(),
            settlement.Payouts.Select(item => new AdminSettlementPayoutDto(
                item.Id,
                item.Amount,
                item.Status.ToString(),
                item.ProviderTransferId,
                item.TransferReference)).ToList()));
    }

    [HttpPost("generate")]
    public async Task<ActionResult<AdminSettlementDto>> Generate(
        [FromBody] GenerateSettlementRequest request,
        CancellationToken cancellationToken)
    {
        var ownerType = Enum.Parse<SettlementOwnerType>(request.OwnerType, true);
        var financialOwnerType = ownerType == SettlementOwnerType.Driver ? FinancialOwnerType.Driver : FinancialOwnerType.Vendor;
        var payableAccount = ownerType == SettlementOwnerType.Driver ? FinancialAccountCode.DriverPayable : FinancialAccountCode.VendorPayable;

        var alreadySettledSourceIds = await context.SettlementItems
            .AsNoTracking()
            .Select(item => item.SourceId)
            .ToListAsync(cancellationToken);

        var lines = await context.JournalLines
            .AsNoTracking()
            .Where(line =>
                line.AccountCode == payableAccount &&
                line.OwnerType == financialOwnerType &&
                line.OwnerId == request.OwnerId &&
                line.CreatedAtUtc >= request.PeriodFrom &&
                line.CreatedAtUtc <= request.PeriodTo &&
                !alreadySettledSourceIds.Contains(line.Id))
            .OrderBy(line => line.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var gross = lines.Sum(line => line.CreditAmount);
        var reversals = lines.Sum(line => line.DebitAmount);
        var net = gross - reversals;

        var settlement = new Settlement(ownerType, request.OwnerId, request.PeriodFrom, request.PeriodTo, SettlementOrigin.ScheduledCycle);
        settlement.UpdateTotals(gross, 0m, refund: reversals);

        context.Settlements.Add(settlement);

        foreach (var line in lines)
        {
            context.SettlementItems.Add(new SettlementItem(
                settlement.Id,
                SettlementItemLineType.Order,
                line.Id,
                line.OrderId,
                line.CreditAmount,
                0m,
                line.DebitAmount,
                0m,
                0m,
                line.CreditAmount - line.DebitAmount));
        }

        if (net > 0 && settlement.ResolutionType == SettlementResolutionType.BankPayout)
        {
            context.Payouts.Add(new Payout(settlement.Id, net));
        }

        await context.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(settlement));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] SettlementResolutionRequest? request, CancellationToken cancellationToken)
    {
        var settlement = await LoadSettlementAsync(id, cancellationToken);
        settlement.Approve(ParseResolution(request?.ResolutionType));
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/hold")]
    public async Task<IActionResult> Hold(Guid id, CancellationToken cancellationToken)
    {
        var settlement = await LoadSettlementAsync(id, cancellationToken);
        settlement.Hold();
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        var settlement = await LoadSettlementAsync(id, cancellationToken);
        settlement.Reject();
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/resolve-dispute")]
    public async Task<IActionResult> ResolveDispute(Guid id, [FromBody] SettlementResolutionRequest? request, CancellationToken cancellationToken)
    {
        var settlement = await LoadSettlementAsync(id, cancellationToken);
        settlement.ResolveDispute(ParseResolution(request?.ResolutionType));
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<Settlement> LoadSettlementAsync(Guid id, CancellationToken cancellationToken) =>
        await context.Settlements.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException($"Settlement {id} was not found.");

    private static SettlementResolutionType? ParseResolution(string? value) =>
        Enum.TryParse<SettlementResolutionType>(value, true, out var parsed) ? parsed : null;

    private static AdminSettlementDto ToDto(Settlement settlement) =>
        new(
            settlement.Id,
            settlement.OwnerType.ToString(),
            settlement.OwnerId,
            settlement.Status.ToString(),
            settlement.ResolutionType.ToString(),
            settlement.PeriodFrom,
            settlement.PeriodTo,
            settlement.GrossAmount,
            settlement.CommissionAmount,
            settlement.RefundAmount,
            settlement.AdjustmentAmount,
            settlement.RecoveryAmount,
            settlement.NetAmount);
}

public sealed record GenerateSettlementRequest(
    string OwnerType,
    Guid OwnerId,
    DateTime PeriodFrom,
    DateTime PeriodTo);

public sealed record SettlementResolutionRequest(string? ResolutionType);

public sealed record AdminSettlementListDto(
    IReadOnlyList<AdminSettlementDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminSettlementDto(
    Guid Id,
    string OwnerType,
    Guid OwnerId,
    string Status,
    string ResolutionType,
    DateTime PeriodFrom,
    DateTime PeriodTo,
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal RefundAmount,
    decimal AdjustmentAmount,
    decimal RecoveryAmount,
    decimal NetAmount);

public sealed record AdminSettlementDetailDto(
    AdminSettlementDto Settlement,
    IReadOnlyList<AdminSettlementItemDto> Items,
    IReadOnlyList<AdminSettlementPayoutDto> Payouts);

public sealed record AdminSettlementItemDto(
    Guid Id,
    string LineType,
    Guid SourceId,
    Guid? OrderId,
    decimal Amount,
    decimal Commission,
    decimal Refund,
    decimal Adjustment,
    decimal Recovery,
    decimal NetAmount);

public sealed record AdminSettlementPayoutDto(
    Guid Id,
    decimal Amount,
    string Status,
    string? ProviderTransferId,
    string? TransferReference);
