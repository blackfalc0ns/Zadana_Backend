using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
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

    public DriverHomeReadService(
        IApplicationDbContext context,
        IDriverRepository driverRepository,
        IDeliveryDispatchService dispatchService,
        IDriverCommitmentPolicyService driverCommitmentPolicyService)
    {
        _context = context;
        _driverRepository = driverRepository;
        _dispatchService = dispatchService;
        _driverCommitmentPolicyService = driverCommitmentPolicyService;
    }

    public async Task<DriverHomeDto> GetHomeAsync(
        Guid driverUserId,
        bool processExpiredOffers = false,
        CancellationToken cancellationToken = default)
    {
        if (processExpiredOffers)
        {
            await _dispatchService.ProcessExpiredOffersAsync(cancellationToken);
        }

        var driver = await _driverRepository.GetByUserIdAsync(driverUserId, cancellationToken)
            ?? throw new NotFoundException("Driver", driverUserId);

        var commitment = await _driverCommitmentPolicyService.GetDriverSummaryAsync(driver.Id, cancellationToken);
        var operationalStatus = DriverOperationalStatusFactory.Create(driver, commitment);

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
                a.OfferExpiresAtUtc.HasValue)
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

        var homeState = ResolveHomeState(operationalStatus, currentOffer, currentAssignment);
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

        var distanceKm = address?.Latitude.HasValue == true && address.Longitude.HasValue
            ? ApproximateDistanceKm(
                assignment.Order.VendorBranch?.Latitude ?? 0m,
                assignment.Order.VendorBranch?.Longitude ?? 0m,
                address.Latitude!.Value,
                address.Longitude!.Value)
            : 0m;

        var countdownSeconds = assignment.OfferExpiresAtUtc.HasValue
            ? Math.Max(0, (int)(assignment.OfferExpiresAtUtc.Value - DateTime.UtcNow).TotalSeconds)
            : 0;

        return new DriverIncomingOfferDto(
            assignment.Id,
            assignment.OrderId,
            assignment.Order.OrderNumber,
            assignment.Order.Vendor.BusinessNameEn,
            assignment.Order.VendorBranch?.AddressLine ?? assignment.Order.Vendor.NationalAddress ?? string.Empty,
            assignment.Order.VendorBranch?.Latitude,
            assignment.Order.VendorBranch?.Longitude,
            address?.ContactName ?? "Customer",
            address?.AddressLine ?? string.Empty,
            address?.Latitude,
            address?.Longitude,
            Math.Round(distanceKm, 2),
            BuildEta(distanceKm),
            assignment.Order.DeliveryFee,
            assignment.Order.PaymentMethod.ToString(),
            assignment.Order.TotalAmount,
            ResolveCodAmount(assignment),
            BuildInitials(assignment.Order.Vendor.BusinessNameEn),
            BuildInitials(address?.ContactName ?? "Customer"),
            assignment.Order.Notes,
            countdownSeconds,
            assignment.Order.Items
                .Select(item => new DriverOfferItemDto(item.ProductName, item.Quantity, assignment.Order.Notes))
                .ToArray());
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

    private static string ResolveHomeState(
        DriverOperationalStatusDto operationalStatus,
        DriverIncomingOfferDto? currentOffer,
        DriverCurrentAssignmentDto? currentAssignment)
    {
        if (currentAssignment is not null)
        {
            return "OnMission";
        }

        if (!operationalStatus.IsOperational)
        {
            return operationalStatus.GateStatus;
        }

        if (currentOffer is not null)
        {
            return "IncomingOffer";
        }

        return operationalStatus.IsAvailable ? "WaitingForOffer" : "Offline";
    }

    private static decimal ApproximateDistanceKm(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        var dLat = (double)(lat2 - lat1) * Math.PI / 180;
        var dLng = (double)(lng2 - lng1) * Math.PI / 180;
        var avgLat = (double)(lat1 + lat2) / 2 * Math.PI / 180;
        var x = dLng * Math.Cos(avgLat);
        var y = dLat;
        return (decimal)(Math.Sqrt(x * x + y * y) * 6371);
    }

    private static string BuildEta(decimal distanceKm)
    {
        var minutes = Math.Max(8, (int)Math.Round((double)distanceKm * 4));
        return $"{minutes}-{minutes + 5} min";
    }

    private static string BuildInitials(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }

    private static decimal ResolveCodAmount(DeliveryAssignment assignment) =>
        assignment.Order.PaymentMethod == PaymentMethodType.CashOnDelivery ? assignment.Order.TotalAmount : 0m;
}
