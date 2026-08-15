using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Identity.Support;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Services;

public sealed class AccountClosureService : IAccountClosureService
{
    private const string RequiredConfirmation = "DELETE";
    private const string DefaultCloseReason = "User requested account deletion";
    private const decimal MoneyEpsilon = 0.009m;

    private static readonly SettlementStatus[] ActiveSettlementStatuses =
    [
        SettlementStatus.Pending,
        SettlementStatus.PendingReview,
        SettlementStatus.Approved,
        SettlementStatus.OnHold,
        SettlementStatus.Processing,
        SettlementStatus.Disputed,
        SettlementStatus.PayoutFailed
    ];

    private readonly IApplicationDbContext _dbContext;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IDriverRepository _driverRepository;
    private readonly IVendorRepository _vendorRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AccountClosureService(
        IApplicationDbContext dbContext,
        IIdentityAccountService identityAccountService,
        IRefreshTokenStore refreshTokenStore,
        IDriverRepository driverRepository,
        IVendorRepository vendorRepository,
        IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _identityAccountService = identityAccountService;
        _refreshTokenStore = refreshTokenStore;
        _driverRepository = driverRepository;
        _vendorRepository = vendorRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task CloseCustomerAccountAsync(
        Guid userId,
        string password,
        string confirmation,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanCloseAsync(userId, UserRole.Customer, password, confirmation, cancellationToken);

        var addresses = await _dbContext.CustomerAddresses
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken);
        if (addresses.Count > 0)
        {
            _dbContext.CustomerAddresses.RemoveRange(addresses);
        }

        var favorites = await _dbContext.CustomerFavorites
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken);
        if (favorites.Count > 0)
        {
            _dbContext.CustomerFavorites.RemoveRange(favorites);
        }

        var carts = await _dbContext.Carts
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken);
        if (carts.Count > 0)
        {
            _dbContext.Carts.RemoveRange(carts);
        }

        await FinalizePlatformClosureAsync(userId, UserRole.Customer, reason, cancellationToken);
    }

    public async Task CloseDriverAccountAsync(
        Guid userId,
        string password,
        string confirmation,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanCloseAsync(userId, UserRole.Driver, password, confirmation, cancellationToken);

        var driver = await _driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        var hasActiveAssignment = await _dbContext.DeliveryAssignments
            .AsNoTracking()
            .AnyAsync(
                assignment =>
                    assignment.DriverId == driver.Id &&
                    DeliveryActiveAssignmentRules.OpenAssignmentStatuses.Contains(assignment.Status) &&
                    !DeliveryActiveAssignmentRules.TerminalOrderStatuses.Contains(assignment.Order.Status),
                cancellationToken);
        if (hasActiveAssignment)
        {
            throw new BusinessRuleException(
                "ACCOUNT_CLOSE_ACTIVE_ASSIGNMENT",
                "ما تقدر تحذف الحساب وفيه طلب توصيل نشط. أكمل الطلب أو ألغِ المهمة أولًا.|You cannot delete the account while you have an active delivery assignment.");
        }

        var hasOpenDispute = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .AnyAsync(
                supportCase =>
                    supportCase.Status != OrderSupportCaseStatus.Rejected &&
                    supportCase.Status != OrderSupportCaseStatus.Resolved &&
                    (
                        supportCase.DriverId == driver.Id ||
                        (supportCase.OrderId != null &&
                         _dbContext.DeliveryAssignments.Any(assignment =>
                             assignment.OrderId == supportCase.OrderId &&
                             assignment.DriverId == driver.Id))
                    ),
                cancellationToken);
        if (hasOpenDispute)
        {
            throw new BusinessRuleException(
                "ACCOUNT_CLOSE_OPEN_DISPUTE",
                "ما تقدر تحذف الحساب وفيه نزاع أو بلاغ مفتوح. انتظر إغلاقه أولًا.|You cannot delete the account while an open dispute or report is still active.");
        }

        var hasActiveWithdrawal = await _dbContext.DriverWithdrawalRequests
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.DriverId == driver.Id &&
                    (item.Status == DriverWithdrawalStatus.Pending ||
                     item.Status == DriverWithdrawalStatus.Processing),
                cancellationToken);
        if (hasActiveWithdrawal)
        {
            throw new BusinessRuleException(
                "ACCOUNT_CLOSE_ACTIVE_WITHDRAWAL",
                "ما تقدر تحذف الحساب وفيه طلب سحب قيد المعالجة. ألغِ الطلب أو انتظر اكتماله.|You cannot delete the account while a withdrawal is pending or processing.");
        }

        await EnsureNoActiveSettlementAsync(SettlementOwnerType.Driver, driver.Id, cancellationToken);
        await EnsureNoActiveWalletHoldAsync(WalletOwnerType.Driver, driver.Id, cancellationToken);

        var wallet = await _dbContext.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.OwnerType == WalletOwnerType.Driver && item.OwnerId == driver.Id,
                cancellationToken);
        EnsureNoWalletFunds(wallet);
        if (wallet is not null && wallet.CodOwedBalance > MoneyEpsilon)
        {
            throw new BusinessRuleException(
                "ACCOUNT_CLOSE_COD_OUTSTANDING",
                "ما تقدر تحذف الحساب وفيه مبالغ دفع عند الاستلام مستحقة.|You cannot delete the account while COD cash is still owed.");
        }

        driver.ToggleAvailability(false);
        if (driver.Status is AccountStatus.Active or AccountStatus.Pending)
        {
            driver.Suspend("Account closed by driver");
        }

        await FinalizePlatformClosureAsync(userId, UserRole.Driver, reason, cancellationToken);
    }

    public async Task CloseVendorAccountAsync(
        Guid userId,
        string password,
        string confirmation,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanCloseAsync(userId, UserRole.Vendor, password, confirmation, cancellationToken);

        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        if (vendor.UserId != userId)
        {
            throw new ForbiddenAccessException(
                "فقط مالك حساب التاجر يقدر يحذف الحساب.|Only the vendor account owner can delete the account.",
                "ACCOUNT_CLOSE_OWNER_ONLY");
        }

        var hasActiveOrders = await _dbContext.Orders
            .AsNoTracking()
            .AnyAsync(
                order =>
                    order.VendorId == vendor.Id &&
                    !DeliveryActiveAssignmentRules.TerminalOrderStatuses.Contains(order.Status),
                cancellationToken);
        if (hasActiveOrders)
        {
            throw new BusinessRuleException(
                "ACCOUNT_CLOSE_ACTIVE_ORDERS",
                "ما تقدر تحذف الحساب وفيه طلبات غير مكتملة. أكملها أو أغلقها أولًا.|You cannot delete the account while you still have open orders.");
        }

        var hasOpenDispute = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .AnyAsync(
                supportCase =>
                    supportCase.Order != null &&
                    supportCase.Order.VendorId == vendor.Id &&
                    supportCase.Status != OrderSupportCaseStatus.Rejected &&
                    supportCase.Status != OrderSupportCaseStatus.Resolved,
                cancellationToken);
        if (hasOpenDispute)
        {
            throw new BusinessRuleException(
                "ACCOUNT_CLOSE_OPEN_DISPUTE",
                "ما تقدر تحذف الحساب وفيه نزاع أو بلاغ مفتوح. انتظر إغلاقه أولًا.|You cannot delete the account while an open dispute or report is still active.");
        }

        await EnsureNoActiveSettlementAsync(SettlementOwnerType.Vendor, vendor.Id, cancellationToken);
        await EnsureNoActiveWalletHoldAsync(WalletOwnerType.Vendor, vendor.Id, cancellationToken);

        var wallet = await _dbContext.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.OwnerType == WalletOwnerType.Vendor && item.OwnerId == vendor.Id,
                cancellationToken);
        EnsureNoWalletFunds(wallet);

        var closeReason = NormalizeReason(reason);
        vendor.Archive(closeReason);

        await FinalizePlatformClosureAsync(userId, UserRole.Vendor, closeReason, cancellationToken);
    }

    private async Task EnsureNoActiveSettlementAsync(
        SettlementOwnerType ownerType,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var hasActiveSettlementOrPayout = await _dbContext.Settlements
            .AsNoTracking()
            .AnyAsync(
                settlement =>
                    settlement.OwnerType == ownerType &&
                    settlement.OwnerId == ownerId &&
                    ActiveSettlementStatuses.Contains(settlement.Status),
                cancellationToken);
        if (hasActiveSettlementOrPayout)
        {
            throw new BusinessRuleException(
                "ACCOUNT_CLOSE_ACTIVE_SETTLEMENT",
                "ما تقدر تحذف الحساب وفيه تسوية أو تحويل قيد المعالجة.|You cannot delete the account while a settlement or transfer is in progress.");
        }
    }

    private async Task EnsureNoActiveWalletHoldAsync(
        WalletOwnerType ownerType,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var activeHoldAmount = await _dbContext.WalletHolds
            .AsNoTracking()
            .Where(hold =>
                hold.OwnerType == ownerType &&
                hold.OwnerId == ownerId &&
                hold.Status == WalletHoldStatus.Active)
            .SumAsync(hold => (decimal?)hold.Amount, cancellationToken) ?? 0m;
        if (activeHoldAmount > MoneyEpsilon)
        {
            throw new BusinessRuleException(
                "ACCOUNT_CLOSE_ACTIVE_HOLD",
                "ما تقدر تحذف الحساب وفيه مبالغ محجوزة على المحفظة.|You cannot delete the account while wallet holds are active.");
        }
    }

    private static void EnsureNoWalletFunds(Wallet? wallet)
    {
        if (wallet is null)
        {
            return;
        }

        if (wallet.CurrentBalance > MoneyEpsilon || wallet.PendingBalance > MoneyEpsilon)
        {
            throw new BusinessRuleException(
                "ACCOUNT_CLOSE_WALLET_BALANCE",
                "ما تقدر تحذف الحساب وفيه رصيد في المحفظة. اسحب الرصيد أو صفّره أولًا.|You cannot delete the account while the wallet still has a balance.");
        }
    }

    private async Task EnsureCanCloseAsync(
        Guid userId,
        UserRole expectedRole,
        string password,
        string confirmation,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmation?.Trim(), RequiredConfirmation, StringComparison.Ordinal))
        {
            throw new BadRequestException(
                "ACCOUNT_CLOSE_CONFIRMATION_REQUIRED",
                "اكتب DELETE للتأكيد على حذف الحساب.|Type DELETE to confirm account deletion.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new BadRequestException(
                "ACCOUNT_CLOSE_PASSWORD_REQUIRED",
                "أدخل كلمة المرور لتأكيد حذف الحساب.|Enter your password to confirm account deletion.");
        }

        var account = await _identityAccountService.FindByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        if (!PlatformRoleMembership.HasAnyRole(account, expectedRole))
        {
            throw new ForbiddenAccessException(
                "هذا المسار غير متاح لنوع الحساب الحالي.|This endpoint is not available for the current account type.",
                "ACCOUNT_CLOSE_ROLE_MISMATCH");
        }

        if (account.ArchivedAtUtc.HasValue || account.AccountStatus == AccountStatus.Inactive)
        {
            throw new BusinessRuleException(
                "ACCOUNT_ALREADY_CLOSED",
                "هذا الحساب محذوف بالفعل.|This account is already deleted.");
        }

        var passwordValid = await _identityAccountService.CheckPasswordAsync(userId, password, cancellationToken);
        if (!passwordValid)
        {
            throw new UnauthorizedException(
                "كلمة المرور غير صحيحة.|The password is incorrect.",
                "ACCOUNT_CLOSE_INVALID_PASSWORD");
        }
    }

    private async Task FinalizePlatformClosureAsync(
        Guid userId,
        UserRole closedRole,
        string? reason,
        CancellationToken cancellationToken)
    {
        var account = await _identityAccountService.FindByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        var remainingRoles = PlatformRoleMembership.OwnedRoles(account)
            .Where(role => role != closedRole)
            .ToArray();

        if (remainingRoles.Length == 0)
        {
            await FinalizeClosureAsync(userId, reason, cancellationToken);
            return;
        }

        var removeResult = await _identityAccountService.RemovePlatformRoleAsync(
            userId,
            closedRole,
            cancellationToken);
        if (!removeResult.Succeeded)
        {
            throw new BusinessRuleException(
                "ACCOUNT_CLOSE_FAILED",
                string.Join(", ", removeResult.Errors ?? ["Unable to close the account."]));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task FinalizeClosureAsync(
        Guid userId,
        string? reason,
        CancellationToken cancellationToken)
    {
        await DeactivatePushDevicesAsync(userId, cancellationToken);

        var archiveResult = await _identityAccountService.ArchiveAsync(
            userId,
            NormalizeReason(reason),
            cancellationToken);
        if (!archiveResult.Succeeded)
        {
            throw new BusinessRuleException(
                "ACCOUNT_CLOSE_FAILED",
                string.Join(", ", archiveResult.Errors ?? ["Unable to close the account."]));
        }

        var anonymizeResult = await _identityAccountService.AnonymizeClosedAccountAsync(userId, cancellationToken);
        if (!anonymizeResult.Succeeded)
        {
            throw new BusinessRuleException(
                "ACCOUNT_CLOSE_ANONYMIZE_FAILED",
                string.Join(", ", anonymizeResult.Errors ?? ["Unable to anonymize the closed account."]));
        }

        await _refreshTokenStore.RevokeAllByUserAsync(userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task DeactivatePushDevicesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var devices = await _dbContext.UserPushDevices
            .Where(device => device.UserId == userId && device.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var device in devices)
        {
            device.Deactivate();
        }
    }

    private static string NormalizeReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? DefaultCloseReason : reason.Trim();
}
