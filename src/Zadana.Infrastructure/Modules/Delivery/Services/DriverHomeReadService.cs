using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Infrastructure.Modules.Delivery.Services;

public sealed class DriverHomeReadService : IDriverHomeReadService
{
    private readonly IApplicationDbContext _context;
    private readonly IDriverRepository _driverRepository;
    private readonly IDeliveryDispatchService _dispatchService;
    private readonly IDriverCommitmentPolicyService _driverCommitmentPolicyService;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public DriverHomeReadService(
        IApplicationDbContext context,
        IDriverRepository driverRepository,
        IDeliveryDispatchService dispatchService,
        IDriverCommitmentPolicyService driverCommitmentPolicyService,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService)
    {
        _context = context;
        _driverRepository = driverRepository;
        _dispatchService = dispatchService;
        _driverCommitmentPolicyService = driverCommitmentPolicyService;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
    }

    public async Task<DriverHomeDto> GetHomeAsync(
        Guid driverUserId,
        bool processExpiredOffers = false,
        CancellationToken cancellationToken = default)
    {
        var driver = await _driverRepository.GetByUserIdAsync(driverUserId, cancellationToken)
            ?? throw new NotFoundException("Driver", driverUserId);

        if (driver.ApplyDocumentExpiryLock())
        {
            await _context.SaveChangesAsync(cancellationToken);
            await DriverExpiryLockNotificationDispatcher.NotifyAsync(
                driver,
                _notificationService,
                _oneSignalPushService,
                cancellationToken);
        }

        var commitment = await _driverCommitmentPolicyService.GetDriverSummaryAsync(driver.Id, cancellationToken);
        var operationalStatus = DriverOperationalStatusFactory.Create(
            driver,
            commitment,
            driver.User.IsLoginLocked,
            driver.User.LockedAtUtc,
            driver.User.LockReason);

        // Query the offer BEFORE processing expirations so the driver sees
        // the offer even if it expired moments ago (the countdown UI handles it).
        var currentOfferEntity = await _context.DeliveryAssignments
            .Include(a => a.Order)
                .ThenInclude(o => o.Vendor)
            .Include(a => a.Order)
                .ThenInclude(o => o.VendorBranch)
            .Include(a => a.Order)
                .ThenInclude(o => o.Items)
            .Where(a =>
                a.DriverId == driver.Id &&
                a.Status == AssignmentStatus.OfferSent &&
                a.OfferExpiresAtUtc.HasValue &&
                a.OfferExpiresAtUtc.Value > DateTime.UtcNow)
            .OrderByDescending(a => a.OfferedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var currentAssignmentEntity = await _context.DeliveryAssignments
            .Include(a => a.Order)
                .ThenInclude(o => o.Vendor)
            .Include(a => a.Order)
                .ThenInclude(o => o.VendorBranch)
            .Include(a => a.Driver)
            .Where(a =>
                a.DriverId == driver.Id &&
                (a.Status == AssignmentStatus.Accepted ||
                 a.Status == AssignmentStatus.PickedUp ||
                 a.Status == AssignmentStatus.ArrivedAtVendor ||
                 a.Status == AssignmentStatus.ArrivedAtCustomer))
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var currentOffer = currentOfferEntity is null
            ? null
            : await BuildIncomingOfferDtoAsync(currentOfferEntity, cancellationToken);

        // Process expired offers AFTER reading the driver's own offer so we don't
        // accidentally expire it before the driver sees it. The background worker
        // handles expiration independently; this is just a best-effort cleanup.
        if (processExpiredOffers)
        {
            await _dispatchService.ProcessExpiredOffersAsync(cancellationToken);
        }

        var currentAssignment = currentAssignmentEntity is null
            ? null
            : await BuildCurrentAssignmentDtoAsync(currentAssignmentEntity, cancellationToken);

        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(
                w => w.OwnerType == WalletOwnerType.Driver && w.OwnerId == driver.Id,
                cancellationToken);

        var earningsToday = wallet is null
            ? 0m
            : await _context.WalletTransactions
                .Where(t =>
                    t.WalletId == wallet.Id &&
                    t.Direction == "IN" &&
                    t.CreatedAtUtc.Date == DateTime.UtcNow.Date)
                .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        var completedTrips = await _context.DeliveryAssignments
            .CountAsync(a =>
                a.DriverId == driver.Id &&
                a.Status == AssignmentStatus.Delivered &&
                a.DeliveredAtUtc.HasValue &&
                a.DeliveredAtUtc.Value.Date == DateTime.UtcNow.Date, cancellationToken);

        var unreadAlerts = await _context.Notifications
            .CountAsync(n => n.UserId == driverUserId && !n.IsRead, cancellationToken);

        var homeState = DriverHomeStateResolver.Resolve(operationalStatus, currentOffer, currentAssignment);
        var profileReadiness = DriverProfileReadinessFactory.BuildHomeReadiness(driver, driver.User);

        return new DriverHomeDto(
            operationalStatus,
            homeState,
            currentOffer,
            currentAssignment,
            new DriverEarningsSummaryDto(Math.Round(earningsToday, 2), completedTrips),
            unreadAlerts,
            commitment,
            profileReadiness);
    }

    private async Task<DriverIncomingOfferDto> BuildIncomingOfferDtoAsync(
        DeliveryAssignment assignment,
        CancellationToken cancellationToken)
    {
        var address = await _context.CustomerAddresses
            .FirstOrDefaultAsync(a => a.Id == assignment.Order.CustomerAddressId, cancellationToken);

        return DriverIncomingOfferFactory.Build(assignment, address);
    }

    private async Task<DriverCurrentAssignmentDto> BuildCurrentAssignmentDtoAsync(
        DeliveryAssignment assignment,
        CancellationToken cancellationToken)
    {
        var address = await _context.CustomerAddresses
            .FirstOrDefaultAsync(a => a.Id == assignment.Order.CustomerAddressId, cancellationToken);

        return new DriverCurrentAssignmentDto(
            assignment.Id,
            assignment.OrderId,
            assignment.Order.OrderNumber,
            assignment.Status.ToString(),
            assignment.Order.Vendor.BusinessNameEn,
            assignment.Order.VendorBranch?.AddressLine ?? assignment.Order.Vendor.NationalAddress ?? string.Empty,
            address?.AddressLine ?? string.Empty,
            assignment.Order.VendorBranch?.Latitude,
            assignment.Order.VendorBranch?.Longitude,
            address?.Latitude,
            address?.Longitude,
            ResolveCodAmount(assignment),
            assignment.CreatedAtUtc,
            assignment.Order.Vendor.ContactPhone,
            assignment.Driver?.VehicleType?.ToString(),
            assignment.Driver?.LicenseNumber,
            assignment.RequiresPickupOtpVerification,
            assignment.RequiresDeliveryOtpVerification,
            assignment.IsInHandoffWindow ? assignment.PickupOtpCode : null);
    }

    private static decimal ResolveCodAmount(DeliveryAssignment assignment) =>
        assignment.Order.PaymentMethod == PaymentMethodType.CashOnDelivery
            ? assignment.CodAmount
            : 0m;
}
