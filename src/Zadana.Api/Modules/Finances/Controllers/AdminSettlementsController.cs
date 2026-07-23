using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Authorization;
using Zadana.Api.Common.Export;
using Zadana.Application.Common.Export;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Identity.Constants;
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
    FinanceOwnerNameResolver financeOwnerNameResolver,
    ISettlementProcessingSettingsService? settlementProcessingSettingsService = null,
    ICurrentUserService? currentUserService = null) : ControllerBase
{
    [HttpGet]
    [RequireAccess(PermissionKeys.Admin.FinancesView)]
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
    [RequireAccess(PermissionKeys.Admin.FinancesView)]
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

    [HttpGet("{id:guid}/statement")]
    [RequireAccess(PermissionKeys.Admin.FinancesExport)]
    public async Task<IActionResult> ExportSettlementStatement(Guid id, CancellationToken cancellationToken)
    {
        var settlement = await context.Settlements
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (settlement is null)
        {
            return NotFound();
        }

        var financialOwnerType = settlement.OwnerType switch
        {
            SettlementOwnerType.Vendor => FinancialOwnerType.Vendor,
            SettlementOwnerType.Driver => FinancialOwnerType.Driver,
            _ => FinancialOwnerType.Platform
        };
        var entityName = await financeOwnerNameResolver.ResolveOwnerNameAsync(
            financialOwnerType,
            settlement.OwnerId,
            cancellationToken) ?? $"{settlement.OwnerType} {settlement.OwnerId:N}";
        var entityCode = $"SET-{settlement.CreatedAtUtc:yyMMdd}-{settlement.Id.ToString("N")[..8].ToUpperInvariant()}";

        var file = PdfExportBuilder.BuildStatement(
            ExportFileResult.StampFileName($"settlement-statement-{settlement.Id:N}", ".pdf"),
            "Settlement Statement",
            subtitle: entityCode,
            meta:
            [
                new ExportKeyValue("Entity", entityName),
                new ExportKeyValue("Code", entityCode),
                new ExportKeyValue("Owner Type", settlement.OwnerType.ToString()),
                new ExportKeyValue("Owner ID", settlement.OwnerId.ToString()),
                new ExportKeyValue("Status", settlement.Status.ToString()),
                new ExportKeyValue("Period From", settlement.PeriodFrom.ToString("o")),
                new ExportKeyValue("Period To", settlement.PeriodTo.ToString("o")),
                new ExportKeyValue("Gross", settlement.GrossAmount.ToString("0.##")),
                new ExportKeyValue("Commission", settlement.CommissionAmount.ToString("0.##")),
                new ExportKeyValue("Refund", settlement.RefundAmount.ToString("0.##")),
                new ExportKeyValue("Adjustment", settlement.AdjustmentAmount.ToString("0.##")),
                new ExportKeyValue("Recovery", settlement.RecoveryAmount.ToString("0.##")),
                new ExportKeyValue("Net", settlement.NetAmount.ToString("0.##"))
            ],
            columns:
            [
                new ExportColumn("Line Type", "lineType"),
                new ExportColumn("Order ID", "orderId"),
                new ExportColumn("Amount", "amount"),
                new ExportColumn("Commission", "commission"),
                new ExportColumn("Refund", "refund"),
                new ExportColumn("Adjustment", "adjustment"),
                new ExportColumn("Recovery", "recovery"),
                new ExportColumn("Net", "net")
            ],
            rows: settlement.Items.Select(item => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
            {
                ["lineType"] = item.LineType.ToString(),
                ["orderId"] = item.OrderId?.ToString() ?? string.Empty,
                ["amount"] = item.Amount.ToString("0.##"),
                ["commission"] = item.Commission.ToString("0.##"),
                ["refund"] = item.Refund.ToString("0.##"),
                ["adjustment"] = item.Adjustment.ToString("0.##"),
                ["recovery"] = item.Recovery.ToString("0.##"),
                ["net"] = item.NetAmount.ToString("0.##")
            }),
            totals:
            [
                new ExportKeyValue("Gross", settlement.GrossAmount.ToString("0.##")),
                new ExportKeyValue("Net", settlement.NetAmount.ToString("0.##"))
            ]);

        return ExportFileResult.From(file);
    }

    [HttpPost("generate")]
    [RequireAccess(PermissionKeys.Admin.FinancesEdit)]
    public async Task<ActionResult<AdminSettlementDto>> Generate(
        [FromBody] GenerateSettlementRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SettlementOwnerType>(request.OwnerType, true, out var ownerType) ||
            !Enum.IsDefined(ownerType))
        {
            throw new BadRequestException(
                "SETTLEMENT_OWNER_TYPE_INVALID",
                "Settlement owner type must be Vendor or Driver.");
        }

        if (ownerType == SettlementOwnerType.Driver)
        {
            throw new BusinessRuleException(
                "DRIVER_WITHDRAWAL_WORKFLOW_REQUIRED",
                "Driver payouts must be created from an approved driver withdrawal request so the wallet hold and immutable bank destination are preserved.");
        }

        // Reject if overlapping settlement period exists for same owner (non-rejected statuses)
        var overlappingSettlement = await context.Settlements
            .AsNoTracking()
            .Where(settlement =>
                settlement.OwnerType == ownerType &&
                settlement.OwnerId == request.OwnerId &&
                settlement.Status != SettlementStatus.Rejected &&
                ((settlement.PeriodFrom <= request.PeriodTo && settlement.PeriodTo >= request.PeriodFrom) ||
                 (request.PeriodFrom <= settlement.PeriodTo && request.PeriodTo >= settlement.PeriodFrom)))
            .FirstOrDefaultAsync(cancellationToken);

        if (overlappingSettlement is not null)
        {
            throw new BusinessRuleException(
                "SETTLEMENT_PERIOD_OVERLAP",
                $"An existing settlement (ID: {overlappingSettlement.Id}) already covers part of this period for this owner.");
        }

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
    [RequireAccess(PermissionKeys.Admin.FinancesApprove)]
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
                // Verify vendor available balance >= NetAmount using wallet + active holds logic
                var wallet = await context.Wallets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(w => w.OwnerType == WalletOwnerType.Vendor && w.OwnerId == settlement.OwnerId, cancellationToken);

                if (wallet is not null)
                {
                    var activeHolds = await context.WalletHolds
                        .AsNoTracking()
                        .Where(hold =>
                            hold.OwnerType == WalletOwnerType.Vendor &&
                            hold.OwnerId == settlement.OwnerId &&
                            hold.Status == WalletHoldStatus.Active)
                        .SumAsync(hold => (decimal?)hold.Amount, cancellationToken) ?? 0m;

                    var availableBalance = Math.Max(0m, wallet.CurrentBalance - wallet.PendingBalance - activeHolds);

                    if (availableBalance < settlement.NetAmount)
                    {
                        throw new BusinessRuleException(
                            "INSUFFICIENT_VENDOR_BALANCE",
                            $"Vendor available balance ({availableBalance}) is less than settlement net amount ({settlement.NetAmount}).");
                    }
                }

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
            var approvingAdminId = currentUserService?.UserId
                ?? throw new UnauthorizedException("ADMIN_NOT_AUTHENTICATED");
            await payoutOrchestrator.TriggerAsync(
                payout.Id,
                approvingAdminId,
                cancellationToken: cancellationToken);
        }

        return Ok(await LoadDetailDtoAsync(settlement.Id, cancellationToken));
    }

    [HttpPost("{id:guid}/hold")]
    [RequireAccess(PermissionKeys.Admin.FinancesApprove)]
    public async Task<IActionResult> Hold(Guid id, CancellationToken cancellationToken)
    {
        var settlement = await LoadSettlementAsync(id, cancellationToken);
        settlement.Hold();
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [RequireAccess(PermissionKeys.Admin.FinancesApprove)]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        var settlement = await LoadSettlementAsync(id, cancellationToken);
        settlement.Reject();
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/resolve-dispute")]
    [RequireAccess(PermissionKeys.Admin.FinancesApprove)]
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

        if (settlement.OwnerType == SettlementOwnerType.Driver)
        {
            throw new BusinessRuleException(
                "DRIVER_WITHDRAWAL_WORKFLOW_REQUIRED",
                "A driver settlement cannot create a payout without a linked withdrawal request and immutable payout destination.");
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
