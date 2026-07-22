using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Authorization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.DTOs;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Identity.Constants;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Finances.Controllers;

[ApiController]
[Route("api/admin/finances/adjustments")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminFinanceAdjustmentsController(
    IApplicationDbContext context,
    FinancialEventPostingService financialEventPostingService,
    WalletProjectionUpdater walletProjectionUpdater,
    FinanceOwnerNameResolver financeOwnerNameResolver) : ControllerBase
{
    [HttpGet]
    [RequireAccess(PermissionKeys.Admin.FinancesView)]
    [ProducesResponseType(typeof(AdminFinancialAdjustmentListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminFinancialAdjustmentListDto>> GetAdjustments(
        [FromQuery] string? ownerType = null,
        [FromQuery] Guid? ownerId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = context.WalletTransactions
            .AsNoTracking()
            .Where(txn => txn.TxnType == WalletTxnType.Adjustment)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(ownerType) || ownerId.HasValue)
        {
            var walletOwnerType = string.IsNullOrWhiteSpace(ownerType)
                ? (WalletOwnerType?)null
                : Enum.TryParse<WalletOwnerType>(ownerType, true, out var parsed)
                    ? parsed
                    : null;

            query = query.Include(txn => txn.Wallet);
            
            if (walletOwnerType.HasValue)
            {
                query = query.Where(txn => txn.Wallet.OwnerType == walletOwnerType.Value);
            }

            if (ownerId.HasValue)
            {
                query = query.Where(txn => txn.Wallet.OwnerId == ownerId.Value);
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var transactions = await query
            .Include(txn => txn.Wallet)
            .OrderByDescending(txn => txn.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Batch resolve owner names
        var vendorIds = transactions
            .Where(txn => txn.Wallet.OwnerType == WalletOwnerType.Vendor)
            .Select(txn => txn.Wallet.OwnerId)
            .Distinct()
            .ToList();

        var driverIds = transactions
            .Where(txn => txn.Wallet.OwnerType == WalletOwnerType.Driver)
            .Select(txn => txn.Wallet.OwnerId)
            .Distinct()
            .ToList();

        var vendorNames = await financeOwnerNameResolver.BatchResolveVendorNamesAsync(vendorIds, cancellationToken);
        var driverNames = await financeOwnerNameResolver.BatchResolveDriverNamesAsync(driverIds, cancellationToken);

        var items = transactions.Select(txn =>
        {
            string? ownerName = txn.Wallet.OwnerType switch
            {
                WalletOwnerType.Vendor => vendorNames.GetValueOrDefault(txn.Wallet.OwnerId),
                WalletOwnerType.Driver => driverNames.GetValueOrDefault(txn.Wallet.OwnerId),
                WalletOwnerType.Platform => "Platform",
                _ => null
            };

            return new AdminFinancialAdjustmentDto(
                txn.Id,
                txn.Wallet.OwnerType.ToString(),
                txn.Wallet.OwnerId,
                ownerName,
                txn.Amount,
                txn.Direction,
                txn.Description,
                txn.CreatedAtUtc);
        }).ToList();

        return Ok(new AdminFinancialAdjustmentListDto(items, page, pageSize, totalCount));
    }

    [HttpPost]
    [RequireAccess(PermissionKeys.Admin.FinancesEdit)]
    [ProducesResponseType(typeof(AdminFinancialAdjustmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminFinancialAdjustmentDto>> CreateAdjustment(
        [FromBody] CreateAdminFinancialAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<WalletOwnerType>(request.OwnerType, true, out var walletOwnerType))
        {
            return BadRequest("Invalid owner type. Must be 'vendor' or 'driver'.");
        }

        if (walletOwnerType != WalletOwnerType.Vendor && walletOwnerType != WalletOwnerType.Driver)
        {
            return BadRequest("Owner type must be 'vendor' or 'driver'.");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("Amount must be greater than zero.");
        }

        var direction = request.Direction?.ToUpperInvariant();
        if (direction != "CREDIT" && direction != "DEBIT")
        {
            return BadRequest("Direction must be 'credit' or 'debit'.");
        }

        // Map credit/debit to IN/OUT
        var txnDirection = direction == "CREDIT" ? "IN" : "OUT";

        // Resolve wallet
        var wallet = await context.Wallets
            .FirstOrDefaultAsync(w => w.OwnerType == walletOwnerType && w.OwnerId == request.OwnerId, cancellationToken);

        if (wallet is null)
        {
            return BadRequest($"Wallet not found for {request.OwnerType} {request.OwnerId}.");
        }

        var financialOwnerType = walletOwnerType switch
        {
            WalletOwnerType.Vendor => FinancialOwnerType.Vendor,
            WalletOwnerType.Driver => FinancialOwnerType.Driver,
            WalletOwnerType.Platform => FinancialOwnerType.Platform,
            _ => throw new BusinessRuleException("INVALID_WALLET_OWNER", "Unsupported wallet owner type.")
        };

        var ownerDebit = txnDirection == "OUT" ? request.Amount : 0m;
        var ownerCredit = txnDirection == "IN" ? request.Amount : 0m;
        var offsetDebit = txnDirection == "IN" ? request.Amount : 0m;
        var offsetCredit = txnDirection == "OUT" ? request.Amount : 0m;

        var memo = string.IsNullOrWhiteSpace(request.Reason)
            ? $"Admin adjustment for {request.OwnerType} {request.OwnerId}"
            : request.Reason.Trim();

        var postingResult = await financialEventPostingService.PostAsync(
            FinancialEventType.FinancialAdjustmentApplied,
            $"admin-adjustment:{wallet.Id:N}:{Guid.NewGuid():N}",
            [
                new JournalLineDraft(
                    FinancialAccountCode.ManualAdjustment,
                    offsetDebit,
                    offsetCredit,
                    Memo: $"Offset for admin wallet adjustment {wallet.Id}"),
                new JournalLineDraft(
                    FinancialAccountCode.ManualAdjustment,
                    ownerDebit,
                    ownerCredit,
                    financialOwnerType,
                    wallet.OwnerId,
                    Memo: memo)
            ],
            description: memo,
            cancellationToken: cancellationToken);

        await walletProjectionUpdater.ApplyJournalEntryAsync(postingResult.JournalEntryId, cancellationToken);

        var txn = await context.WalletTransactions
            .AsNoTracking()
            .Include(item => item.Wallet)
            .Where(item => item.WalletId == wallet.Id && item.ReferenceType == "JournalLine")
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstAsync(cancellationToken);

        var ownerName = await financeOwnerNameResolver.ResolveOwnerNameAsync(financialOwnerType, wallet.OwnerId, cancellationToken);

        return Ok(new AdminFinancialAdjustmentDto(
            txn.Id,
            txn.Wallet.OwnerType.ToString(),
            txn.Wallet.OwnerId,
            ownerName,
            txn.Amount,
            txn.Direction,
            txn.Description,
            txn.CreatedAtUtc));
    }
}
