using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Api.Security;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Commands.RegisterDriver;
using Zadana.Application.Modules.Delivery.Commands.ResendAssignmentOtp;
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
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Api.Modules.Delivery.Controllers;

[Route("api/drivers")]
[Tags("Driver App API")]
public class DriversController : ApiControllerBase
{
    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("register")]
    public async Task<IActionResult> RegisterDriver([FromBody] RegisterDriverRequest request)
    {
        DriverVehicleType? parsedVehicleType = null;
        if (!string.IsNullOrWhiteSpace(request.VehicleType))
        {
            if (!DriverVehicleTypeMapper.TryParse(request.VehicleType, out var resolvedVehicleType))
            {
                throw new BusinessRuleException("INVALID_VEHICLE_TYPE", "نوع المركبة غير مدعوم | Unsupported vehicle type.");
            }

            parsedVehicleType = resolvedVehicleType;
        }

        var command = new RegisterDriverCommand(
            request.FullName,
            request.Email,
            request.Phone,
            request.Password,
            parsedVehicleType,
            request.NationalId,
            request.LicenseNumber,
            request.NationalIdExpiryDate,
            request.DriverLicenseExpiryDate,
            request.VehicleLicenseNumber,
            request.VehicleLicenseExpiryDate,
            request.Address,
            request.Region,
            request.City,
            request.NationalIdFrontImageUrl,
            request.NationalIdBackImageUrl,
            request.LicenseImageUrl,
            request.VehicleImageUrl,
            request.PersonalPhotoUrl);

        var result = await Sender.Send(command);
        return Ok(result);
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
        return Ok(DriverOperationalStatusFactory.Create(
            driver,
            commitment,
            driver.User.IsLoginLocked,
            driver.User.LockedAtUtc,
            driver.User.LockReason));
    }

    [HttpGet("home")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverHomeDto>> GetHome(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverHomeReadService driverHomeReadService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        // Keep the mobile home endpoint read-only and fast. Expired offer cleanup is handled by
        // DeliveryDispatchWorker; running it here can make every app open trigger heavy dispatch
        // work, duplicate transient notifications, and proxy timeouts/503s under load.
        return Ok(await driverHomeReadService.GetHomeAsync(userId, processExpiredOffers: false, cancellationToken));
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
        return Ok(new { message_ar = request.IsAvailable ? LocalizedMessages.GetAr(LocalizedMessages.DriverAvailabilityOn) : LocalizedMessages.GetAr(LocalizedMessages.DriverAvailabilityOff), message_en = request.IsAvailable ? LocalizedMessages.GetEn(LocalizedMessages.DriverAvailabilityOn) : LocalizedMessages.GetEn(LocalizedMessages.DriverAvailabilityOff) });
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

        return Ok(new { message_ar = LocalizedMessages.GetAr(LocalizedMessages.DriverLocationUpdated), message_en = LocalizedMessages.GetEn(LocalizedMessages.DriverLocationUpdated) });
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
        var operationalStatus = DriverOperationalStatusFactory.Create(
            driver,
            commitment,
            driver.User.IsLoginLocked,
            driver.User.LockedAtUtc,
            driver.User.LockReason);

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
                restrictionMessageAr = operationalStatus.RestrictionMessageAr,
                restrictionMessageEn = operationalStatus.RestrictionMessageEn,
                message = operationalStatus.Message,
                messageAr = operationalStatus.MessageAr,
                messageEn = operationalStatus.MessageEn
            });
        }

        var assignment = await context.DeliveryAssignments
            .Include(a => a.Order)
            .Where(a => a.DriverId == driver.Id &&
                a.Status != Domain.Modules.Delivery.Enums.AssignmentStatus.Delivered &&
                a.Status != Domain.Modules.Delivery.Enums.AssignmentStatus.Failed &&
                a.Status != Domain.Modules.Delivery.Enums.AssignmentStatus.Cancelled &&
                a.Status != Domain.Modules.Delivery.Enums.AssignmentStatus.Returned &&
                a.Status != Domain.Modules.Delivery.Enums.AssignmentStatus.Rejected &&
                a.Order.Status != Domain.Modules.Orders.Enums.OrderStatus.Cancelled &&
                a.Order.Status != Domain.Modules.Orders.Enums.OrderStatus.VendorRejected &&
                a.Order.Status != Domain.Modules.Orders.Enums.OrderStatus.DeliveryFailed &&
                a.Order.Status != Domain.Modules.Orders.Enums.OrderStatus.Refunded &&
                a.Order.Status != Domain.Modules.Orders.Enums.OrderStatus.Delivered)
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
                restrictionMessageAr = operationalStatus.RestrictionMessageAr,
                restrictionMessageEn = operationalStatus.RestrictionMessageEn,
                message = operationalStatus.Message,
                messageAr = operationalStatus.MessageAr,
                messageEn = operationalStatus.MessageEn
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
                codAmount = ResolveCodAmount(assignment),
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
                "تحتاج مراجعة واعتماد من الإدارة قبل قبول العروض | Your account must be reviewed and approved by admin before accepting offers.");
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
                "تحتاج مراجعة واعتماد من الإدارة قبل رفض العروض | Your account must be reviewed and approved by admin before rejecting offers.");
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
                "تحتاج مراجعة واعتماد من الإدارة قبل إرسال إثبات التوصيل | Your account must be reviewed and approved by admin before submitting delivery proof.");
        }

        var assignmentExists = await context.DeliveryAssignments
            .AnyAsync(a => a.Id == assignmentId && a.DriverId == driver.Id, cancellationToken);

        if (!assignmentExists)
        {
            throw new BusinessRuleException("ASSIGNMENT_NOT_OWNED", "تقدر ترسل إثبات فقط للطلبات المخصصة لك | You can only submit proof for your assigned deliveries.");
        }

        var proofId = await Sender.Send(
            new SubmitDeliveryProofCommand(
                assignmentId, request.ProofType, request.ImageUrl,
                request.OtpCode, request.RecipientName, request.Note),
            cancellationToken);

        return Ok(new { id = proofId, message_ar = LocalizedMessages.GetAr(LocalizedMessages.DeliveryProofSubmitted), message_en = LocalizedMessages.GetEn(LocalizedMessages.DeliveryProofSubmitted) });
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

    [HttpPost("assignments/{assignmentId:guid}/resend-otp")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> ResendOtp(
        Guid assignmentId,
        [FromBody] DriverResendOtpRequest? request,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OtpType))
        {
            throw new BadRequestException("INVALID_OTP_TYPE", "OTP type is required.");
        }

        await Sender.Send(
            new ResendAssignmentOtpCommand(assignmentId, userId, request.OtpType),
            cancellationToken);

        return Ok(new 
        { 
            message_ar = "أرسلنا رمز التحقق بنجاح",
            message_en = LocalizedMessages.GetEn(LocalizedMessages.OtpResentSuccessfully) 
        });
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
                a.Order.PaymentMethod == PaymentMethodType.CashOnDelivery ? a.Order.TotalAmount : 0m))
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
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        return Ok(await driverReadService.GetCompletedOrdersAsync(driver.Id, status, page, perPage, cancellationToken));
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
        return Ok(new DriverOrderStatusResponse(result.OrderId, result.Status, result.MessageAr, result.MessageEn, result.UpdatedAssignment));
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
        return Ok(new DriverArrivalStateResponse(
            result.OrderId,
            result.AssignmentId,
            result.ArrivalState,
            result.MessageAr,
            result.MessageEn,
            result.UpdatedAssignment));
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
        return Ok(new DriverOrderStatusResponse(result.OrderId, result.Status, result.MessageAr, result.MessageEn, result.UpdatedAssignment));
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
        return Ok(new DriverArrivalStateResponse(
            result.OrderId,
            result.AssignmentId,
            result.ArrivalState,
            result.MessageAr,
            result.MessageEn,
            result.UpdatedAssignment));
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
        return Ok(new DriverOrderStatusResponse(result.OrderId, result.Status, result.MessageAr, result.MessageEn, result.UpdatedAssignment));
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
        return Ok(new DriverOrderStatusResponse(result.OrderId, result.Status, result.MessageAr, result.MessageEn, result.UpdatedAssignment));
    }

    private static decimal ResolveCodAmount(DeliveryAssignment assignment) =>
        assignment.Order.PaymentMethod == PaymentMethodType.CashOnDelivery ? assignment.Order.TotalAmount : 0m;

}

public record DriverOrderStatusResponse(
    Guid OrderId,
    string Status,
    string MessageAr,
    string MessageEn,
    DriverAssignmentDetailDto? UpdatedAssignment = null);

public record DriverArrivalStateResponse(
    Guid OrderId,
    Guid AssignmentId,
    string ArrivalState,
    string MessageAr,
    string MessageEn,
    DriverAssignmentDetailDto? UpdatedAssignment = null);

public record DriverDeliveryFailedRequest(string? Note);
public record DriverOfferRejectRequest(string? Reason);
public record SetAvailabilityRequest(bool IsAvailable);
public record UpdateLocationRequest(decimal Latitude, decimal Longitude, decimal? AccuracyMeters);
public record SubmitProofRequest(string ProofType, string? ImageUrl, string? OtpCode, string? RecipientName, string? Note);
public record DriverOtpVerificationRequest(string OtpType, string OtpCode);
public record DriverResendOtpRequest(string? OtpType);
