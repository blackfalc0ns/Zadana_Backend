using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Finances.Controllers;

[ApiController]
[Route("api/admin/settlements")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminSettlementsController(
    IApplicationDbContext context,
    PayoutOrchestrator payoutOrchestrator,
    VendorPayoutWalletService vendorPayoutWalletService,
    ISettlementProcessingSettingsService? settlementProcessingSettingsService = null) : ControllerBase
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
            .Select(item => new AdminSettlementDto(
                item.Id,
                item.OwnerType.ToString(),
                item.OwnerId,
                item.Status.ToString(),
                item.ResolutionType.ToString(),
                item.PeriodFrom,
                item.PeriodTo,
                item.GrossAmount,
                item.CommissionAmount,
                item.RefundAmount,
                item.AdjustmentAmount,
                item.RecoveryAmount,
                item.NetAmount,
                item.CreatedAtUtc,
                item.ProcessedAtUtc,
                item.Items.Count))
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
                .ThenInclude(item => item.ManualConfirmation)
            .Include(item => item.Payouts)
                .ThenInclude(item => item.ExecutionReservation)
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
            settlement.Payouts.Select(ToPayoutDto).ToList(),
            await GetSettlementProcessingModeAsync(cancellationToken)));
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

        // The recipient has to be captured at approval time, after the verified
        // payout account is resolved. Creating a destination-less payout here
        // would allow a later account edit to change where the funds go.

        await context.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(settlement));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<AdminSettlementDetailDto>> Approve(Guid id, [FromBody] SettlementResolutionRequest? request, CancellationToken cancellationToken)
    {
        var settlement = await LoadSettlementForApprovalAsync(id, cancellationToken);
        var resolution = ParseResolution(request?.ResolutionType);
        settlement.Approve(resolution);

        Payout? payout = null;
        if (settlement.NetAmount > 0 && settlement.ResolutionType == SettlementResolutionType.BankPayout)
        {
            payout = await EnsureSettlementPayoutAsync(settlement, cancellationToken);

            if (settlement.OwnerType == SettlementOwnerType.Vendor)
            {
                await vendorPayoutWalletService.EnsureHoldAsync(
                    settlement.OwnerId,
                    settlement.Id,
                    payout.Amount,
                    "AdminSettlementApproval",
                    $"Hold for approved settlement {settlement.Id}",
                    cancellationToken);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        if (payout is not null &&
            request?.TriggerPayout != false &&
            await payoutOrchestrator.IsAutomaticProcessingEnabledAsync(cancellationToken))
        {
            await payoutOrchestrator.TriggerAsync(payout.Id, cancellationToken: cancellationToken);
        }

        return Ok(await LoadDetailDtoAsync(settlement.Id, cancellationToken));
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

    private async Task<Settlement> LoadSettlementForApprovalAsync(Guid id, CancellationToken cancellationToken) =>
        await context.Settlements
            .Include(item => item.Payouts)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException($"Settlement {id} was not found.");

    private async Task<Payout> EnsureSettlementPayoutAsync(Settlement settlement, CancellationToken cancellationToken)
    {
        var existingPayout = settlement.Payouts
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault(item => item.Status is not PayoutStatus.Cancelled);

        if (existingPayout is not null)
        {
            return existingPayout;
        }

        Guid? vendorBankAccountId = null;
        if (settlement.OwnerType == SettlementOwnerType.Vendor)
        {
            var bankAccount = await context.VendorBankAccounts
                .AsNoTracking()
                .Where(item =>
                    item.VendorId == settlement.OwnerId &&
                    item.IsPrimary &&
                    item.Status == BankAccountStatus.Verified)
                .OrderByDescending(item => item.VerifiedAtUtc)
                .ThenByDescending(item => item.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleException(
                    "VENDOR_VERIFIED_BANK_ACCOUNT_REQUIRED",
                    "Vendor must have a verified primary bank account before approving bank payout settlement.");

            if (!IsValidSaudiIban(bankAccount.IBAN))
            {
                throw new BusinessRuleException(
                    "VENDOR_BANK_IBAN_INVALID",
                    "Vendor primary bank account must be a valid Saudi IBAN before approving bank payout settlement.");
            }

            vendorBankAccountId = bankAccount.Id;
        }

        var payout = new Payout(settlement.Id, settlement.NetAmount, vendorBankAccountId);
        if (settlement.OwnerType == SettlementOwnerType.Vendor)
        {
            var bankAccount = await context.VendorBankAccounts
                .AsNoTracking()
                .FirstAsync(item => item.Id == vendorBankAccountId!.Value, cancellationToken);
            payout.PrepareDestination(
                PayoutDestinationType.VendorBankAccount,
                PayoutDestinationSnapshotCodec.CreateVendorBankAccount(bankAccount));
        }

        var scheduledPayoutDay = settlement.OwnerType switch
        {
            SettlementOwnerType.Vendor => await context.Vendors
                .AsNoTracking()
                .Where(item => item.Id == settlement.OwnerId)
                .Select(item => (PayoutScheduleDay?)item.PayoutDay)
                .FirstOrDefaultAsync(cancellationToken),
            SettlementOwnerType.Driver => await context.Drivers
                .AsNoTracking()
                .Where(item => item.Id == settlement.OwnerId)
                .Select(item => (PayoutScheduleDay?)item.PayoutDay)
                .FirstOrDefaultAsync(cancellationToken),
            _ => null
        };
        if (scheduledPayoutDay.HasValue)
        {
            payout.SetScheduledPayoutDay(scheduledPayoutDay.Value);
        }

        context.Payouts.Add(payout);
        return payout;
    }

    private async Task<AdminSettlementDetailDto> LoadDetailDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var settlement = await context.Settlements
            .AsNoTracking()
            .Include(item => item.Items)
            .Include(item => item.Payouts)
                .ThenInclude(item => item.ManualConfirmation)
            .Include(item => item.Payouts)
                .ThenInclude(item => item.ExecutionReservation)
            .FirstAsync(item => item.Id == id, cancellationToken);

        return new AdminSettlementDetailDto(
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
            settlement.Payouts.Select(ToPayoutDto).ToList(),
            await GetSettlementProcessingModeAsync(cancellationToken));
    }

    private async Task<string> GetSettlementProcessingModeAsync(CancellationToken cancellationToken) =>
        settlementProcessingSettingsService is null
            ? SettlementProcessingMode.Automatic.ToString()
            : (await settlementProcessingSettingsService.GetAsync(cancellationToken)).Mode.ToString();

    private static AdminSettlementPayoutDto ToPayoutDto(Payout payout) =>
        new(
            payout.Id,
            payout.Amount,
            payout.Status.ToString(),
            payout.ProviderTransferId,
            payout.TransferReference,
            payout.ManualConfirmation is null
                ? null
                : new AdminManualPayoutConfirmationDto(
                    payout.ManualConfirmation.Id,
                    payout.ManualConfirmation.TransferReference,
                    payout.ManualConfirmation.ProofAttachmentId,
                    !string.IsNullOrWhiteSpace(payout.ManualConfirmation.LegacyProofUrl),
                    payout.ManualConfirmation.ConfirmedByUserId,
                    payout.ManualConfirmation.ConfirmedAtUtc),
            payout.ExecutionReservation is null
                ? null
                : new AdminPayoutExecutionReservationDto(
                    payout.ExecutionReservation.Id,
                    payout.ExecutionReservation.Mode.ToString(),
                    payout.ExecutionReservation.Status.ToString(),
                    payout.ExecutionReservation.ClaimedByUserId,
                    payout.ExecutionReservation.ClaimedAtUtc,
                    payout.ExecutionReservation.SubmittedByUserId,
                    payout.ExecutionReservation.SubmittedAtUtc,
                    payout.ExecutionReservation.SubmissionReference,
                    payout.ExecutionReservation.ReleasedByUserId,
                    payout.ExecutionReservation.ReleasedAtUtc,
                    payout.ExecutionReservation.ReleaseReason),
            PayoutDestinationSnapshotCodec.ToMaskedLabel(payout.DestinationSnapshot),
            payout.ScheduledPayoutDay?.ToString());

    private static SettlementResolutionType? ParseResolution(string? value) =>
        Enum.TryParse<SettlementResolutionType>(value, true, out var parsed) ? parsed : null;

    private static bool IsValidSaudiIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return false;
        }

        var clean = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return clean.Length == 24 &&
            clean.StartsWith("SA", StringComparison.OrdinalIgnoreCase) &&
            clean.Skip(2).All(char.IsDigit);
    }

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
            settlement.NetAmount,
            settlement.CreatedAtUtc,
            settlement.ProcessedAtUtc,
            settlement.Items.Count);
}

public sealed record GenerateSettlementRequest(
    string OwnerType,
    Guid OwnerId,
    DateTime PeriodFrom,
    DateTime PeriodTo);

public sealed record SettlementResolutionRequest(string? ResolutionType, bool? TriggerPayout = true);

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
    decimal NetAmount,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    int ItemCount);

public sealed record AdminSettlementDetailDto(
    AdminSettlementDto Settlement,
    IReadOnlyList<AdminSettlementItemDto> Items,
    IReadOnlyList<AdminSettlementPayoutDto> Payouts,
    string SettlementProcessingMode);

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
    string? TransferReference,
    AdminManualPayoutConfirmationDto? ManualConfirmation,
    AdminPayoutExecutionReservationDto? ExecutionReservation,
    string? DestinationMaskedLabel,
    string? ScheduledPayoutDay);
