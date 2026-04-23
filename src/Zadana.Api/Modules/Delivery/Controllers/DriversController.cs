using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Commands.RegisterDriver;
using Zadana.Application.Modules.Delivery.Commands.SetDriverZone;
using Zadana.Application.Modules.Delivery.Commands.SubmitDeliveryProof;
using Zadana.Application.Modules.Delivery.Commands.UpdateDriverAvailability;
using Zadana.Application.Modules.Delivery.Commands.UpdateDriverLocation;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Commands.DriverUpdateOrderStatus;
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
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        return Ok(DriverOperationalStatusFactory.Create(driver));
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
            new UpdateDriverLocationCommand(driver.Id, request.Latitude, request.Longitude),
            cancellationToken);

        return Ok(new { message = "Location updated" });
    }

    [HttpGet("assignments/current")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> GetCurrentAssignment(
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
            return Ok(new
            {
                hasAssignment = false,
                gateStatus = DriverOperationalStatusFactory.ResolveGateStatus(driver),
                isOperational = false,
                verificationStatus = driver.VerificationStatus.ToString(),
                accountStatus = driver.Status.ToString(),
                message = DriverOperationalStatusFactory.ResolveMessage(driver)
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

}

public record DriverOrderStatusResponse(Guid OrderId, string Status, string Message);
public record DriverDeliveryFailedRequest(string? Note);
public record SetDriverZoneRequest(Guid ZoneId);
public record SetAvailabilityRequest(bool IsAvailable);
public record UpdateLocationRequest(decimal Latitude, decimal Longitude);
public record SubmitProofRequest(string ProofType, string? ImageUrl, string? OtpCode, string? RecipientName, string? Note);
