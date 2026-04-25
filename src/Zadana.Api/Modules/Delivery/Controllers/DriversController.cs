using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Commands.RegisterDriver;
using Zadana.Application.Modules.Delivery.Commands.SetDriverZone;
using Zadana.Application.Modules.Delivery.Commands.SubmitDeliveryProof;
using Zadana.Application.Modules.Delivery.Commands.UpdateDriverArrivalState;
using Zadana.Application.Modules.Delivery.Commands.UpdateDriverAvailability;
using Zadana.Application.Modules.Delivery.Commands.UpdateDriverLocation;
using Zadana.Application.Modules.Delivery.Commands.VerifyAssignmentOtp;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Commands.DriverUpdateOrderStatus;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Api.Modules.Delivery.Controllers;

[Route("api/drivers")]
[Tags("Driver App API")]
public class DriversController : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> RegisterDriver([FromBody] RegisterDriverRequest request)
    {
        var command = new RegisterDriverCommand(
            request.FullName,
            request.Email,
            request.Phone,
            request.Password,
            request.VehicleType,
            request.NationalId,
            request.LicenseNumber,
            request.Address,
            request.PrimaryZoneId,
            request.NationalIdImageUrl,
            request.LicenseImageUrl,
            request.VehicleImageUrl,
            request.PersonalPhotoUrl);

        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpGet("~/api/public/delivery-zones")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicZones(
        [FromServices] IDriverReadService driverReadService,
        CancellationToken cancellationToken = default)
    {
        var zones = await driverReadService.GetActiveZonesAsync(cancellationToken);
        return Ok(zones);
    }

    [HttpGet("zones")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> GetZones(
        [FromServices] IDriverReadService driverReadService,
        CancellationToken cancellationToken = default)
    {
        var zones = await driverReadService.GetActiveZonesAsync(cancellationToken);
        return Ok(zones);
    }

    [HttpGet("me/status")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverOperationalStatusDto>> GetMyStatus(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IDriverCommitmentPolicyService driverCommitmentPolicyService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        var commitment = await driverCommitmentPolicyService.GetDriverSummaryAsync(driver.Id, cancellationToken);
        return Ok(DriverOperationalStatusFactory.Create(driver, commitment));
    }

    [HttpGet("home")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverHomeDto>> GetHome(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromServices] IDeliveryDispatchService dispatchService,
        [FromServices] IDriverCommitmentPolicyService driverCommitmentPolicyService,
        CancellationToken cancellationToken = default)
    {
        await dispatchService.ProcessExpiredOffersAsync(cancellationToken);

        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        var commitment = await driverCommitmentPolicyService.GetDriverSummaryAsync(driver.Id, cancellationToken);
        var operationalStatus = DriverOperationalStatusFactory.Create(driver, commitment);

        var currentOfferEntity = await context.DeliveryAssignments
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

        var currentAssignmentEntity = await context.DeliveryAssignments
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
            : await BuildIncomingOfferDtoAsync(context, currentOfferEntity, cancellationToken);

        var currentAssignment = currentAssignmentEntity is null
            ? null
            : await BuildCurrentAssignmentDtoAsync(context, currentAssignmentEntity, cancellationToken);

        var wallet = await context.Wallets
            .FirstOrDefaultAsync(w => w.OwnerType == Domain.Modules.Wallets.Enums.WalletOwnerType.Driver && w.OwnerId == driver.Id, cancellationToken);

        var earningsToday = wallet is null
            ? 0m
            : await context.WalletTransactions
                .Where(t =>
                    t.WalletId == wallet.Id &&
                    t.Direction == "IN" &&
                    t.CreatedAtUtc.Date == DateTime.UtcNow.Date)
                .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        var completedTrips = await context.DeliveryAssignments
            .CountAsync(a =>
                a.DriverId == driver.Id &&
                a.Status == AssignmentStatus.Delivered &&
                a.DeliveredAtUtc.HasValue &&
                a.DeliveredAtUtc.Value.Date == DateTime.UtcNow.Date, cancellationToken);

        var unreadAlerts = await context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

        var homeState = ResolveHomeState(operationalStatus, currentOffer, currentAssignment);

        return Ok(new DriverHomeDto(
            operationalStatus,
            homeState,
            currentOffer,
            currentAssignment,
            new DriverEarningsSummaryDto(Math.Round(earningsToday, 2), completedTrips),
            unreadAlerts,
            commitment));
    }

    [HttpPut("me/zone")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> SetZone(
        [FromBody] SetDriverZoneRequest request,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        await Sender.Send(new SetDriverZoneCommand(userId, request.ZoneId), cancellationToken);
        return Ok(new { message = "Zone updated successfully" });
    }

    [HttpPut("me/availability")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> SetAvailability(
        [FromBody] SetAvailabilityRequest request,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        await Sender.Send(new UpdateDriverAvailabilityCommand(userId, request.IsAvailable), cancellationToken);
        return Ok(new { message = $"Availability set to {request.IsAvailable}" });
    }

    [HttpPost("location")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> UpdateLocation(
        [FromBody] UpdateLocationRequest request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        await Sender.Send(
            new UpdateDriverLocationCommand(driver.Id, request.Latitude, request.Longitude, request.AccuracyMeters),
            cancellationToken);

        return Ok(new { message = "Location updated" });
    }

    [HttpGet("assignments/current")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> GetCurrentAssignment(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromServices] IDriverCommitmentPolicyService driverCommitmentPolicyService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);
        var commitment = await driverCommitmentPolicyService.GetDriverSummaryAsync(driver.Id, cancellationToken);
        var operationalStatus = DriverOperationalStatusFactory.Create(driver, commitment);

        if (!driver.CanReceiveOrders)
        {
            return Ok(new
            {
                hasAssignment = false,
                gateStatus = operationalStatus.GateStatus,
                isOperational = operationalStatus.IsOperational,
                verificationStatus = operationalStatus.VerificationStatus,
                accountStatus = operationalStatus.AccountStatus,
                commitmentScore = operationalStatus.CommitmentScore,
                dailyRejections = operationalStatus.DailyRejections,
                weeklyRejections = operationalStatus.WeeklyRejections,
                enforcementLevel = operationalStatus.EnforcementLevel,
                canReceiveOffers = operationalStatus.CanReceiveOffers,
                restrictionMessage = operationalStatus.RestrictionMessage,
                message = operationalStatus.Message
            });
        }

        var assignment = await context.DeliveryAssignments
            .Include(a => a.Order)
            .Where(a => a.DriverId == driver.Id &&
                a.Status != Domain.Modules.Delivery.Enums.AssignmentStatus.Delivered &&
                a.Status != Domain.Modules.Delivery.Enums.AssignmentStatus.Failed &&
                a.Status != Domain.Modules.Delivery.Enums.AssignmentStatus.Cancelled)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment is null && !operationalStatus.CanReceiveOffers)
        {
            return Ok(new
            {
                hasAssignment = false,
                gateStatus = operationalStatus.GateStatus,
                isOperational = operationalStatus.IsOperational,
                verificationStatus = operationalStatus.VerificationStatus,
                accountStatus = operationalStatus.AccountStatus,
                commitmentScore = operationalStatus.CommitmentScore,
                dailyRejections = operationalStatus.DailyRejections,
                weeklyRejections = operationalStatus.WeeklyRejections,
                enforcementLevel = operationalStatus.EnforcementLevel,
                canReceiveOffers = operationalStatus.CanReceiveOffers,
                restrictionMessage = operationalStatus.RestrictionMessage,
                message = operationalStatus.Message
            });
        }

        if (assignment is null) return Ok(new { hasAssignment = false });

        return Ok(new
        {
            hasAssignment = true,
            assignment = new
            {
                assignment.Id,
                assignment.OrderId,
                orderNumber = assignment.Order.OrderNumber,
                status = assignment.Status.ToString(),
                assignment.CodAmount,
                assignment.CreatedAtUtc
            }
        });
    }

    [HttpGet("assignments/{assignmentId:guid}")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverAssignmentDetailDto>> GetAssignmentDetail(
        Guid assignmentId,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IDriverReadService driverReadService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        var detail = await driverReadService.GetAssignmentDetailAsync(driver.Id, assignmentId, cancellationToken)
            ?? throw new NotFoundException("DeliveryAssignment", assignmentId);

        return Ok(detail);
    }

    [HttpPost("offers/{assignmentId:guid}/accept")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverOfferActionResultDto>> AcceptOffer(
        Guid assignmentId,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IDeliveryDispatchService dispatchService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        if (!driver.CanReceiveOrders)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_READY_FOR_DISPATCH",
                "Driver must be reviewed and approved by admin before accepting delivery offers.");
        }

        return Ok(await dispatchService.AcceptOfferAsync(assignmentId, driver.Id, cancellationToken));
    }

    [HttpPost("offers/{assignmentId:guid}/reject")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverOfferActionResultDto>> RejectOffer(
        Guid assignmentId,
        [FromBody] DriverOfferRejectRequest? request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IDeliveryDispatchService dispatchService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        if (!driver.CanReceiveOrders)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_READY_FOR_DISPATCH",
                "Driver must be reviewed and approved by admin before rejecting delivery offers.");
        }

        return Ok(await dispatchService.RejectOfferAsync(assignmentId, driver.Id, request?.Reason, cancellationToken));
    }

    [HttpPost("assignments/{assignmentId:guid}/proof")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> SubmitProof(
        Guid assignmentId,
        [FromBody] SubmitProofRequest request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        if (!driver.CanReceiveOrders)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_READY_FOR_DISPATCH",
                "Driver must be reviewed and approved by admin before submitting delivery proof.");
        }

        var assignmentExists = await context.DeliveryAssignments
            .AnyAsync(a => a.Id == assignmentId && a.DriverId == driver.Id, cancellationToken);

        if (!assignmentExists)
        {
            throw new BusinessRuleException("ASSIGNMENT_NOT_OWNED", "You can only submit proof for your assigned deliveries.");
        }

        var proofId = await Sender.Send(
            new SubmitDeliveryProofCommand(
                assignmentId, request.ProofType, request.ImageUrl,
                request.OtpCode, request.RecipientName, request.Note),
            cancellationToken);

        return Ok(new { id = proofId, message = "Proof submitted successfully" });
    }

    [HttpPost("assignments/{assignmentId:guid}/verify-otp")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverOtpVerificationResultDto>> VerifyOtp(
        Guid assignmentId,
        [FromBody] DriverOtpVerificationRequest request,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new VerifyAssignmentOtpCommand(assignmentId, userId, request.OtpType, request.OtpCode),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("assignments/history")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> GetAssignmentHistory(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        if (!driver.CanReceiveOrders)
        {
            return Ok(Array.Empty<AdminDriverAssignmentDto>());
        }

        var assignments = await context.DeliveryAssignments
            .Include(a => a.Order)
            .Where(a => a.DriverId == driver.Id)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(50)
            .Select(a => new AdminDriverAssignmentDto(
                a.Id,
                a.OrderId,
                a.Order.OrderNumber,
                a.Status.ToString(),
                a.AcceptedAtUtc,
                a.DeliveredAtUtc,
                a.FailedAtUtc,
                a.FailureReason,
                a.CodAmount))
            .ToArrayAsync(cancellationToken);

        return Ok(assignments);
    }

    [HttpGet("orders/completed")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverCompletedOrdersListDto>> GetCompletedOrders(
        [FromQuery] string? status,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IDriverReadService driverReadService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        return Ok(await driverReadService.GetCompletedOrdersAsync(driver.Id, status, cancellationToken));
    }

    [HttpGet("orders/completed/{orderId:guid}")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverCompletedOrderDetailDto>> GetCompletedOrderDetail(
        Guid orderId,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IDriverReadService driverReadService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        var detail = await driverReadService.GetCompletedOrderDetailAsync(driver.Id, orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        return Ok(detail);
    }

    // --- Order Status Endpoints ---

    [HttpPost("orders/{orderId:guid}/picked-up")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverOrderStatusResponse>> MarkPickedUp(
        Guid orderId,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new DriverUpdateOrderStatusCommand(orderId, userId, OrderStatus.PickedUp, "Driver picked up the order"),
            cancellationToken);
        return Ok(new DriverOrderStatusResponse(result.OrderId, result.Status, result.Message));
    }

    [HttpPost("orders/{orderId:guid}/arrived-at-vendor")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverArrivalStateResponse>> MarkArrivedAtVendor(
        Guid orderId,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new UpdateDriverArrivalStateCommand(orderId, userId, "arrived_at_vendor"),
            cancellationToken);
        return Ok(new DriverArrivalStateResponse(result.OrderId, result.AssignmentId, result.ArrivalState, result.Message));
    }

    [HttpPost("orders/{orderId:guid}/on-the-way")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverOrderStatusResponse>> MarkOnTheWay(
        Guid orderId,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new DriverUpdateOrderStatusCommand(orderId, userId, OrderStatus.OnTheWay, "Driver is on the way"),
            cancellationToken);
        return Ok(new DriverOrderStatusResponse(result.OrderId, result.Status, result.Message));
    }

    [HttpPost("orders/{orderId:guid}/arrived-at-customer")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverArrivalStateResponse>> MarkArrivedAtCustomer(
        Guid orderId,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new UpdateDriverArrivalStateCommand(orderId, userId, "arrived_at_customer"),
            cancellationToken);
        return Ok(new DriverArrivalStateResponse(result.OrderId, result.AssignmentId, result.ArrivalState, result.Message));
    }

    [HttpPost("orders/{orderId:guid}/delivered")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverOrderStatusResponse>> MarkDelivered(
        Guid orderId,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new DriverUpdateOrderStatusCommand(orderId, userId, OrderStatus.Delivered, "Order delivered successfully"),
            cancellationToken);
        return Ok(new DriverOrderStatusResponse(result.OrderId, result.Status, result.Message));
    }

    [HttpPost("orders/{orderId:guid}/delivery-failed")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverOrderStatusResponse>> MarkDeliveryFailed(
        Guid orderId,
        [FromBody] DriverDeliveryFailedRequest? request,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new DriverUpdateOrderStatusCommand(orderId, userId, OrderStatus.DeliveryFailed, request?.Note),
            cancellationToken);
        return Ok(new DriverOrderStatusResponse(result.OrderId, result.Status, result.Message));
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

    private static async Task<DriverIncomingOfferDto> BuildIncomingOfferDtoAsync(
        IApplicationDbContext context,
        DeliveryAssignment assignment,
        CancellationToken cancellationToken)
    {
        var address = await context.CustomerAddresses
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
            BuildInitials(assignment.Order.Vendor.BusinessNameEn),
            BuildInitials(address?.ContactName ?? "Customer"),
            assignment.Order.Notes,
            countdownSeconds,
            assignment.Order.Items
                .Select(item => new DriverOfferItemDto(item.ProductName, item.Quantity, assignment.Order.Notes))
                .ToArray());
    }

    private static async Task<DriverCurrentAssignmentDto> BuildCurrentAssignmentDtoAsync(
        IApplicationDbContext context,
        DeliveryAssignment assignment,
        CancellationToken cancellationToken)
    {
        var address = await context.CustomerAddresses
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
            assignment.CodAmount,
            assignment.CreatedAtUtc,
            assignment.Order.Vendor.ContactPhone,
            assignment.Driver?.VehicleType?.ToString(),
            assignment.Driver?.LicenseNumber,
            assignment.RequiresPickupOtpVerification,
            assignment.RequiresDeliveryOtpVerification);
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

}

public record DriverOrderStatusResponse(Guid OrderId, string Status, string Message);
public record DriverArrivalStateResponse(Guid OrderId, Guid AssignmentId, string ArrivalState, string Message);
public record DriverDeliveryFailedRequest(string? Note);
public record DriverOfferRejectRequest(string? Reason);
public record SetDriverZoneRequest(Guid ZoneId);
public record SetAvailabilityRequest(bool IsAvailable);
public record UpdateLocationRequest(decimal Latitude, decimal Longitude, decimal? AccuracyMeters);
public record SubmitProofRequest(string ProofType, string? ImageUrl, string? OtpCode, string? RecipientName, string? Note);
public record DriverOtpVerificationRequest(string OtpType, string OtpCode);
