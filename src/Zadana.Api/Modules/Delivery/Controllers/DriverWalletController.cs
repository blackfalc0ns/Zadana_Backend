using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Wallets.DTOs;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Delivery.Controllers;

[Route("api/drivers/wallet")]
[Tags("Driver App API")]
[Authorize(Policy = "DriverOnly")]
public class DriverWalletController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DriverWalletSummaryDto>> GetWallet(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var wallet = await GetOrCreateWalletAsync(context, driver.Id, cancellationToken);

        var todayStart = DateTime.UtcNow.Date;
        var weekStart = DateTime.UtcNow.AddDays(-7);
        var monthStart = DateTime.UtcNow.AddDays(-30);

        var todayEarnings = await SumIncomingAsync(context, wallet.Id, todayStart, cancellationToken);
        var weekEarnings = await SumIncomingAsync(context, wallet.Id, weekStart, cancellationToken);
        var monthEarnings = await SumIncomingAsync(context, wallet.Id, monthStart, cancellationToken);

        var recentTransactions = await context.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == wallet.Id)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(10)
            .Select(MapTransaction())
            .ToListAsync(cancellationToken);

        var paymentMethods = await context.DriverPayoutMethods
            .AsNoTracking()
            .Where(m => m.DriverId == driver.Id)
            .OrderByDescending(m => m.IsPrimary)
            .ThenByDescending(m => m.CreatedAtUtc)
            .Select(MapPayoutMethod())
            .ToListAsync(cancellationToken);

        var withdrawalSummary = await BuildWithdrawalSummaryAsync(context, driver.Id, cancellationToken);

        return Ok(new DriverWalletSummaryDto(
            wallet.CurrentBalance,
            wallet.CurrentBalance,
            wallet.PendingBalance,
            todayEarnings,
            weekEarnings,
            monthEarnings,
            recentTransactions,
            paymentMethods,
            withdrawalSummary));
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<DriverWalletTransactionListDto>> GetTransactions(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var wallet = await GetOrCreateWalletAsync(context, driver.Id, cancellationToken);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == wallet.Id)
            .OrderByDescending(t => t.CreatedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapTransaction())
            .ToListAsync(cancellationToken);

        return Ok(new DriverWalletTransactionListDto(items, page, pageSize, totalCount));
    }

    [HttpGet("payment-methods")]
    public async Task<ActionResult<IReadOnlyList<DriverPayoutMethodDto>>> GetPaymentMethods(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var methods = await context.DriverPayoutMethods
            .AsNoTracking()
            .Where(m => m.DriverId == driver.Id)
            .OrderByDescending(m => m.IsPrimary)
            .ThenByDescending(m => m.CreatedAtUtc)
            .Select(MapPayoutMethod())
            .ToListAsync(cancellationToken);

        return Ok(methods);
    }

    [HttpPost("payment-methods")]
    public async Task<ActionResult<DriverPayoutMethodDto>> CreatePaymentMethod(
        [FromBody] CreateDriverPayoutMethodRequest request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var methodType = ParseMethodType(request.Type);
        var existingMethods = await context.DriverPayoutMethods
            .Where(m => m.DriverId == driver.Id)
            .ToListAsync(cancellationToken);

        var shouldBePrimary = request.IsPrimary || existingMethods.Count == 0;
        if (shouldBePrimary)
        {
            foreach (var method in existingMethods.Where(m => m.IsPrimary))
            {
                method.UnsetPrimary();
            }
        }

        var payoutMethod = new DriverPayoutMethod(
            driver.Id,
            methodType,
            request.AccountHolderName,
            request.AccountIdentifier,
            request.ProviderName,
            shouldBePrimary);

        context.DriverPayoutMethods.Add(payoutMethod);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(MapPayoutMethodDto(payoutMethod));
    }

    [HttpPut("payment-methods/{id:guid}")]
    public async Task<ActionResult<DriverPayoutMethodDto>> UpdatePaymentMethod(
        Guid id,
        [FromBody] UpdateDriverPayoutMethodRequest request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var payoutMethod = await context.DriverPayoutMethods
            .FirstOrDefaultAsync(m => m.Id == id && m.DriverId == driver.Id, cancellationToken)
            ?? throw new NotFoundException("DriverPayoutMethod", id);

        payoutMethod.UpdateDetails(
            ParseMethodType(request.Type),
            request.AccountHolderName,
            request.AccountIdentifier,
            request.ProviderName);

        await context.SaveChangesAsync(cancellationToken);

        return Ok(MapPayoutMethodDto(payoutMethod));
    }

    [HttpDelete("payment-methods/{id:guid}")]
    public async Task<IActionResult> DeletePaymentMethod(
        Guid id,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var payoutMethod = await context.DriverPayoutMethods
            .FirstOrDefaultAsync(m => m.Id == id && m.DriverId == driver.Id, cancellationToken)
            ?? throw new NotFoundException("DriverPayoutMethod", id);

        var isPrimary = payoutMethod.IsPrimary;
        context.DriverPayoutMethods.Remove(payoutMethod);

        if (isPrimary)
        {
            var fallbackPrimary = await context.DriverPayoutMethods
                .Where(m => m.DriverId == driver.Id && m.Id != id)
                .OrderByDescending(m => m.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            fallbackPrimary?.SetPrimary();
        }

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("payment-methods/{id:guid}/make-primary")]
    public async Task<ActionResult<DriverPayoutMethodDto>> MakePrimary(
        Guid id,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var methods = await context.DriverPayoutMethods
            .Where(m => m.DriverId == driver.Id)
            .ToListAsync(cancellationToken);

        var payoutMethod = methods.FirstOrDefault(m => m.Id == id)
            ?? throw new NotFoundException("DriverPayoutMethod", id);

        foreach (var method in methods)
        {
            method.UnsetPrimary();
        }

        payoutMethod.SetPrimary();
        await context.SaveChangesAsync(cancellationToken);

        return Ok(MapPayoutMethodDto(payoutMethod));
    }

    [HttpPost("withdrawals")]
    public async Task<ActionResult<DriverWithdrawalRequestDto>> CreateWithdrawal(
        [FromBody] CreateDriverWithdrawalRequest request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        var wallet = await GetOrCreateWalletAsync(context, driver.Id, cancellationToken);

        var payoutMethod = request.PaymentMethodId.HasValue
            ? await context.DriverPayoutMethods.FirstOrDefaultAsync(
                m => m.Id == request.PaymentMethodId.Value && m.DriverId == driver.Id,
                cancellationToken)
            : await context.DriverPayoutMethods.FirstOrDefaultAsync(
                m => m.DriverId == driver.Id && m.IsPrimary,
                cancellationToken);

        if (payoutMethod is null)
        {
            throw new BusinessRuleException("DRIVER_PAYOUT_METHOD_REQUIRED", "أضف طريقة سحب أساسية قبل طلب السحب | Add a primary payout method before requesting a withdrawal.");
        }

        if (wallet.CurrentBalance < request.Amount)
        {
            throw new BusinessRuleException("INSUFFICIENT_WITHDRAWABLE_BALANCE", "مبلغ السحب يتجاوز الرصيد المتاح | Withdrawal amount exceeds available balance.");
        }

        wallet.Hold(request.Amount);
        context.WalletTransactions.Add(new WalletTransaction(
            wallet.Id,
            WalletTxnType.Hold,
            request.Amount,
            "OUT",
            description: "Driver withdrawal request submitted"));

        var withdrawal = new DriverWithdrawalRequest(driver.Id, wallet.Id, payoutMethod.Id, request.Amount);
        context.DriverWithdrawalRequests.Add(withdrawal);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(MapWithdrawalDto(withdrawal, payoutMethod));
    }

    [HttpGet("withdrawals")]
    public async Task<ActionResult<DriverWithdrawalRequestListDto>> GetWithdrawals(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(currentUserService, driverRepository, cancellationToken);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.DriverWithdrawalRequests
            .AsNoTracking()
            .Include(w => w.DriverPayoutMethod)
            .Where(w => w.DriverId == driver.Id)
            .OrderByDescending(w => w.CreatedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new DriverWithdrawalRequestListDto(
            items.Select(item => MapWithdrawalDto(item, item.DriverPayoutMethod)).ToList(),
            page,
            pageSize,
            totalCount));
    }

    private static async Task<Domain.Modules.Delivery.Entities.Driver> GetDriverAsync(
        ICurrentUserService currentUserService,
        IDriverRepository driverRepository,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        return await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);
    }

    private static async Task<Wallet> GetOrCreateWalletAsync(
        IApplicationDbContext context,
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var wallet = await context.Wallets
            .FirstOrDefaultAsync(w => w.OwnerType == WalletOwnerType.Driver && w.OwnerId == driverId, cancellationToken);

        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new Wallet(WalletOwnerType.Driver, driverId);
        context.Wallets.Add(wallet);
        await context.SaveChangesAsync(cancellationToken);
        return wallet;
    }

    private static async Task<decimal> SumIncomingAsync(
        IApplicationDbContext context,
        Guid walletId,
        DateTime fromUtc,
        CancellationToken cancellationToken)
    {
        return await context.WalletTransactions
            .Where(t => t.WalletId == walletId && t.Direction == "IN" && t.CreatedAtUtc >= fromUtc)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;
    }

    private static async Task<DriverWithdrawalSummaryDto> BuildWithdrawalSummaryAsync(
        IApplicationDbContext context,
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var query = context.DriverWithdrawalRequests
            .AsNoTracking()
            .Where(w => w.DriverId == driverId);

        var pendingQuery = query.Where(w =>
            w.Status == DriverWithdrawalStatus.Pending ||
            w.Status == DriverWithdrawalStatus.Processing);

        var pendingCount = await pendingQuery.CountAsync(cancellationToken);
        var pendingAmount = await pendingQuery.SumAsync(w => (decimal?)w.Amount, cancellationToken) ?? 0m;
        var totalRequests = await query.CountAsync(cancellationToken);

        return new DriverWithdrawalSummaryDto(pendingCount, pendingAmount, totalRequests);
    }

    private static DriverPayoutMethodType ParseMethodType(string value)
    {
        if (!Enum.TryParse<DriverPayoutMethodType>(value, true, out var methodType))
        {
            throw new BusinessRuleException("INVALID_DRIVER_PAYOUT_METHOD_TYPE", "نوع طريقة السحب غير مدعوم | Unsupported payout method type.");
        }

        return methodType;
    }

    private static Expression<Func<WalletTransaction, DriverWalletTransactionDto>> MapTransaction() =>
        item => new DriverWalletTransactionDto(
            item.Id,
            item.TxnType.ToString(),
            item.Direction,
            item.Amount,
            item.Description,
            item.ReferenceType,
            item.ReferenceId.HasValue ? item.ReferenceId.Value.ToString() : null,
            item.CreatedAtUtc);

    private static Expression<Func<DriverPayoutMethod, DriverPayoutMethodDto>> MapPayoutMethod() =>
        item => new DriverPayoutMethodDto(
            item.Id,
            item.MethodType.ToString(),
            item.AccountHolderName,
            item.ProviderName,
            item.MaskedLabel,
            item.IsPrimary,
            item.IsVerified);

    private static DriverPayoutMethodDto MapPayoutMethodDto(DriverPayoutMethod item) =>
        new(
            item.Id,
            item.MethodType.ToString(),
            item.AccountHolderName,
            item.ProviderName,
            item.MaskedLabel,
            item.IsPrimary,
            item.IsVerified);

    private static DriverWithdrawalRequestDto MapWithdrawalDto(
        DriverWithdrawalRequest withdrawal,
        DriverPayoutMethod payoutMethod) =>
        new(
            withdrawal.Id,
            withdrawal.Amount,
            withdrawal.Status.ToString(),
            withdrawal.TransferReference,
            withdrawal.FailureReason,
            withdrawal.CreatedAtUtc,
            withdrawal.ProcessedAtUtc,
            MapPayoutMethodDto(payoutMethod));
}
