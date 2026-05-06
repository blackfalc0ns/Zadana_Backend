using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Wallets.DTOs;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Wallets.Controllers;

[Route("api/admin/wallets")]
[Tags("Admin Wallet Management API")]
[Authorize(Policy = "AdminOnly")]
public class AdminWalletsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminWalletListDto>> GetWallets(
        [FromServices] IApplicationDbContext context,
        [FromQuery] string? ownerType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.Wallets.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(ownerType) && Enum.TryParse<WalletOwnerType>(ownerType, true, out var type))
        {
            query = query.Where(w => w.OwnerType == type);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        
        var wallets = await query
            .OrderByDescending(w => w.CurrentBalance)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Fetch owner details efficiently
        var vendorIds = wallets.Where(w => w.OwnerType == WalletOwnerType.Vendor).Select(w => w.OwnerId).ToList();
        var driverIds = wallets.Where(w => w.OwnerType == WalletOwnerType.Driver).Select(w => w.OwnerId).ToList();

        var vendors = await context.Vendors.AsNoTracking()
            .Where(v => vendorIds.Contains(v.Id))
            .ToDictionaryAsync(
                v => v.Id,
                v => new
                {
                    Name = !string.IsNullOrWhiteSpace(v.BusinessNameAr)
                        ? v.BusinessNameAr
                        : !string.IsNullOrWhiteSpace(v.BusinessNameEn)
                            ? v.BusinessNameEn
                            : "Unknown Vendor",
                    Phone = v.ContactPhone
                },
                cancellationToken);

        var drivers = await context.Drivers.AsNoTracking()
            .Include(d => d.User)
            .Where(d => driverIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => new { Name = d.User.FullName, Phone = d.User.PhoneNumber ?? "" }, cancellationToken);

        var items = wallets.Select(w =>
        {
            string ownerName = "Unknown";
            string ownerPhone = "";
            
            if (w.OwnerType == WalletOwnerType.Vendor && vendors.TryGetValue(w.OwnerId, out var vendor))
            {
                ownerName = vendor.Name;
                ownerPhone = vendor.Phone;
            }
            else if (w.OwnerType == WalletOwnerType.Driver && drivers.TryGetValue(w.OwnerId, out var driver))
            {
                ownerName = driver.Name;
                ownerPhone = driver.Phone;
            }

            return new AdminWalletSummaryDto(
                w.Id,
                w.OwnerType.ToString(),
                w.OwnerId,
                ownerName,
                ownerPhone,
                w.CurrentBalance,
                w.PendingBalance,
                w.CreatedAtUtc
            );
        }).ToList();

        var totalPlatformBalance = await context.Wallets.SumAsync(w => (decimal?)w.CurrentBalance, cancellationToken) ?? 0m;
        var totalPendingWithdrawals = await context.Wallets.SumAsync(w => (decimal?)w.PendingBalance, cancellationToken) ?? 0m;

        return Ok(new AdminWalletListDto(items, page, pageSize, totalCount, totalPlatformBalance, totalPendingWithdrawals));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminWalletSummaryDto>> GetWallet(
        Guid id,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var wallet = await context.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException("Wallet", id);

        string ownerName = "Unknown";
        string ownerPhone = "";

        if (wallet.OwnerType == WalletOwnerType.Vendor)
        {
            var vendor = await context.Vendors.AsNoTracking().FirstOrDefaultAsync(v => v.Id == wallet.OwnerId, cancellationToken);
            ownerName = vendor is null
                ? "Unknown Vendor"
                : !string.IsNullOrWhiteSpace(vendor.BusinessNameAr)
                    ? vendor.BusinessNameAr
                    : !string.IsNullOrWhiteSpace(vendor.BusinessNameEn)
                        ? vendor.BusinessNameEn
                        : "Unknown Vendor";
            ownerPhone = vendor?.ContactPhone ?? string.Empty;
        }
        else if (wallet.OwnerType == WalletOwnerType.Driver)
        {
            var driver = await context.Drivers.AsNoTracking().Include(d => d.User).FirstOrDefaultAsync(d => d.Id == wallet.OwnerId, cancellationToken);
            ownerName = driver?.User.FullName ?? "Unknown Driver";
            ownerPhone = driver?.User.PhoneNumber ?? "";
        }

        return Ok(new AdminWalletSummaryDto(
            wallet.Id,
            wallet.OwnerType.ToString(),
            wallet.OwnerId,
            ownerName,
            ownerPhone,
            wallet.CurrentBalance,
            wallet.PendingBalance,
            wallet.CreatedAtUtc
        ));
    }

    [HttpGet("{id:guid}/transactions")]
    public async Task<ActionResult<AdminWalletTransactionListDto>> GetTransactions(
        Guid id,
        [FromServices] IApplicationDbContext context,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.WalletTransactions.AsNoTracking().Where(t => t.WalletId == id);
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new AdminWalletTransactionDto(
                t.Id,
                t.TxnType.ToString(),
                t.Direction,
                t.Amount,
                t.Description,
                t.ReferenceType,
                t.ReferenceId.HasValue ? t.ReferenceId.Value.ToString() : null,
                t.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return Ok(new AdminWalletTransactionListDto(items, page, pageSize, totalCount));
    }

    [HttpPost("{id:guid}/adjustments")]
    public async Task<ActionResult<AdminWalletTransactionDto>> CreateAdjustment(
        Guid id,
        [FromBody] AdminCreateAdjustmentRequest request,
        [FromServices] IApplicationDbContext context,
        [FromServices] INotificationService notificationService,
        CancellationToken cancellationToken = default)
    {
        var wallet = await context.Wallets.FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException("Wallet", id);

        if (request.Direction == "IN")
        {
            wallet.Credit(request.Amount);
        }
        else
        {
            wallet.Debit(request.Amount);
        }

        var txn = new WalletTransaction(
            wallet.Id,
            WalletTxnType.Adjustment,
            request.Amount,
            request.Direction,
            description: request.Description,
            referenceType: "AdminAdjustment"
        );

        context.WalletTransactions.Add(txn);
        await context.SaveChangesAsync(cancellationToken);

        if (wallet.OwnerType == WalletOwnerType.Driver)
        {
            var driverUserId = await context.Drivers
                .AsNoTracking()
                .Where(driver => driver.Id == wallet.OwnerId)
                .Select(driver => driver.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (driverUserId != Guid.Empty)
            {
                var data = DriverNotificationDataBuilder.Build(
                    screen: "wallet",
                    @event: "wallet.admin_adjustment",
                    extra: new
                    {
                        walletId = wallet.Id,
                        amount = txn.Amount,
                        direction = txn.Direction,
                        transactionId = txn.Id
                    });

                await notificationService.SendToUserAsync(
                    driverUserId,
                    new NotificationDispatchRequest(
                        "تم تعديل رصيد المحفظة",
                        "Wallet balance adjusted",
                        "تم تعديل رصيد محفظتك من قبل الإدارة.",
                        "Your wallet balance was adjusted by the team.",
                        NotificationTypes.DriverWalletUpdated,
                        NotificationCategories.Wallet,
                        NotificationPriorities.Normal,
                        txn.Id,
                        data),
                    cancellationToken);

                await notificationService.SendDriverWalletUpdatedAsync(driverUserId, cancellationToken);
            }
        }

        return Ok(new AdminWalletTransactionDto(
            txn.Id,
            txn.TxnType.ToString(),
            txn.Direction,
            txn.Amount,
            txn.Description,
            txn.ReferenceType,
            txn.ReferenceId.HasValue ? txn.ReferenceId.Value.ToString() : null,
            txn.CreatedAtUtc
        ));
    }

    [HttpGet("withdrawals")]
    public async Task<ActionResult<AdminWithdrawalRequestListDto>> GetWithdrawals(
        [FromServices] IApplicationDbContext context,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.DriverWithdrawalRequests.AsNoTracking()
            .Include(w => w.DriverPayoutMethod)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Zadana.Domain.Modules.Wallets.Enums.DriverWithdrawalStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(w => w.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        
        var withdrawals = await query
            .OrderByDescending(w => w.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var driverIds = withdrawals.Select(w => w.DriverId).Distinct().ToList();
        var drivers = await context.Drivers.AsNoTracking()
            .Include(d => d.User)
            .Where(d => driverIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => new { Name = d.User.FullName, Phone = d.User.PhoneNumber ?? "" }, cancellationToken);

        var items = withdrawals.Select(w =>
        {
            var driverInfo = drivers.GetValueOrDefault(w.DriverId);
            return new AdminDriverWithdrawalRequestDto(
                w.Id,
                w.DriverId,
                driverInfo?.Name ?? "Unknown",
                driverInfo?.Phone ?? "",
                w.Amount,
                w.Status.ToString(),
                w.TransferReference,
                w.FailureReason,
                w.CreatedAtUtc,
                w.ProcessedAtUtc,
                new AdminDriverPayoutMethodDto(
                    w.DriverPayoutMethod.Id,
                    w.DriverPayoutMethod.MethodType.ToString(),
                    w.DriverPayoutMethod.AccountHolderName,
                    w.DriverPayoutMethod.ProviderName ?? string.Empty,
                    w.DriverPayoutMethod.MaskedLabel
                )
            );
        }).ToList();

        return Ok(new AdminWithdrawalRequestListDto(items, page, pageSize, totalCount));
    }

    [HttpPost("withdrawals/{id:guid}/process")]
    public async Task<IActionResult> ProcessWithdrawal(
        Guid id,
        [FromBody] AdminProcessWithdrawalRequest request,
        [FromServices] IApplicationDbContext context,
        [FromServices] INotificationService notificationService,
        [FromServices] IOneSignalPushService oneSignalPushService,
        CancellationToken cancellationToken = default)
    {
        var withdrawal = await context.DriverWithdrawalRequests
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException("DriverWithdrawalRequest", id);

        if (withdrawal.Status != Zadana.Domain.Modules.Wallets.Enums.DriverWithdrawalStatus.Pending && 
            withdrawal.Status != Zadana.Domain.Modules.Wallets.Enums.DriverWithdrawalStatus.Processing)
        {
            throw new BusinessRuleException("INVALID_STATUS", "Only pending or processing withdrawals can be processed.");
        }

        var wallet = await context.Wallets.FirstOrDefaultAsync(w => w.Id == withdrawal.WalletId, cancellationToken)
            ?? throw new NotFoundException("Wallet", withdrawal.WalletId);

        if (request.IsApproved)
        {
            wallet.SettleHold(withdrawal.Amount);
            withdrawal.MarkPaid(request.TransferReference);
            
            var txn = new WalletTransaction(
                wallet.Id,
                WalletTxnType.Payout,
                withdrawal.Amount,
                "OUT",
                description: "Withdrawal processed",
                referenceType: "DriverWithdrawal",
                referenceId: withdrawal.Id
            );
            context.WalletTransactions.Add(txn);
        }
        else
        {
            wallet.ReleaseHold(withdrawal.Amount);
            withdrawal.MarkFailed(request.FailureReason ?? "Rejected by admin");
            context.WalletTransactions.Add(new WalletTransaction(
                wallet.Id,
                WalletTxnType.Release,
                withdrawal.Amount,
                "IN",
                description: "Withdrawal rejected and balance released",
                referenceType: "DriverWithdrawal",
                referenceId: withdrawal.Id
            ));
        }

        await context.SaveChangesAsync(cancellationToken);

        var driverUserId = await context.Drivers
            .AsNoTracking()
            .Where(driver => driver.Id == withdrawal.DriverId)
            .Select(driver => driver.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (driverUserId != Guid.Empty)
        {
            var eventName = request.IsApproved ? "wallet.withdrawal_paid" : "wallet.withdrawal_rejected";
            var titleAr = request.IsApproved ? "تم تحويل مبلغ السحب" : "تم رفض طلب السحب";
            var titleEn = request.IsApproved ? "Withdrawal paid" : "Withdrawal rejected";
            var bodyAr = request.IsApproved
                ? $"تمت معالجة طلب السحب رقم #{withdrawal.Id} بنجاح."
                : $"تم رفض طلب السحب رقم #{withdrawal.Id}.";
            var bodyEn = request.IsApproved
                ? $"Your withdrawal request #{withdrawal.Id} was paid successfully."
                : $"Your withdrawal request #{withdrawal.Id} was rejected.";

            var data = DriverNotificationDataBuilder.Build(
                screen: "wallet",
                @event: eventName,
                withdrawalId: withdrawal.Id,
                extra: new
                {
                    amount = withdrawal.Amount,
                    status = withdrawal.Status.ToString(),
                    transferReference = withdrawal.TransferReference,
                    failureReason = withdrawal.FailureReason
                });

            await notificationService.SendToUserAsync(
                driverUserId,
                new NotificationDispatchRequest(
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    NotificationTypes.DriverWalletUpdated,
                    NotificationCategories.Wallet,
                    NotificationPriorities.High,
                    withdrawal.Id,
                    data),
                cancellationToken);

            await notificationService.SendDriverWalletUpdatedAsync(driverUserId, cancellationToken);

            await oneSignalPushService.SendMobileNotificationAsync(
                OneSignalMobilePushRequest.CreateStandard(
                    driverUserId.ToString(),
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    NotificationTypes.DriverWalletUpdated,
                    withdrawal.Id,
                    data,
                    category: NotificationCategories.Wallet),
                cancellationToken);
        }

        return NoContent();
    }
}
