using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.ResendAssignmentOtp;

public record ResendAssignmentOtpCommand(
    Guid AssignmentId,
    Guid DriverUserId,
    string OtpType) : IRequest;

public class ResendAssignmentOtpCommandValidator : AbstractValidator<ResendAssignmentOtpCommand>
{
    public ResendAssignmentOtpCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
        RuleFor(x => x.OtpType)
            .NotEmpty()
            .Must(BeSupportedOtpType)
            .WithMessage("OTP type must be pickup or delivery.");
    }

    private static bool BeSupportedOtpType(string? type) =>
        string.Equals(type, "pickup", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "delivery", StringComparison.OrdinalIgnoreCase);
}

public class ResendAssignmentOtpCommandHandler : IRequestHandler<ResendAssignmentOtpCommand>
{
    private static readonly TimeSpan OtpTtl = TimeSpan.FromHours(12);

    private readonly IApplicationDbContext _context;
    private readonly IDriverRepository _driverRepository;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public ResendAssignmentOtpCommandHandler(
        IApplicationDbContext context,
        IDriverRepository driverRepository,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService)
    {
        _context = context;
        _driverRepository = driverRepository;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
    }

    public async Task Handle(ResendAssignmentOtpCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByUserIdAsync(request.DriverUserId, cancellationToken)
            ?? throw new BusinessRuleException("DRIVER_NOT_FOUND", "No driver profile found for the current user.");

        var assignment = await _context.DeliveryAssignments
            .Include(a => a.Order)
                .ThenInclude(o => o.Vendor)
            .Include(a => a.Driver)
                .ThenInclude(d => d!.User)
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken)
            ?? throw new NotFoundException("DeliveryAssignment", request.AssignmentId);

        if (assignment.DriverId != driver.Id)
        {
            throw new BusinessRuleException("ASSIGNMENT_NOT_OWNED", "You can only resend OTP for your assigned deliveries.");
        }

        var otpType = request.OtpType.Trim().ToLowerInvariant();

        if (otpType == "pickup")
        {
            if (assignment.IsPickupOtpVerified)
            {
                throw new BusinessRuleException("PICKUP_OTP_ALREADY_VERIFIED", "Pickup OTP has already been verified.");
            }

            assignment.RegeneratePickupOtp(OtpTtl);
            await _context.SaveChangesAsync(cancellationToken);

            if (assignment.Order.Vendor is not null)
            {
                var driverName = assignment.Driver?.User.FullName ?? "Assigned driver";
                var driverPhone = assignment.Driver?.User.PhoneNumber ?? string.Empty;
                var (vehicleTypeAr, vehicleTypeEn) = DriverVehicleTypeDisplay.LocalizePair(assignment.Driver?.VehicleType);
                var plateNumber = assignment.Driver?.LicenseNumber ?? "N/A";

                await _notificationService.SendToUserAsync(
                    assignment.Order.Vendor.UserId,
                    "إعادة إرسال رمز الاستلام",
                    "Pickup OTP Resent",
                    $"المندوب {driverName} ({vehicleTypeAr} - {plateNumber}) متواجد لاستلام طلب {assignment.Order.OrderNumber}. افتح تفاصيل الطلب لعرض رمز الاستلام المؤمّن.",
                    $"Driver {driverName} ({vehicleTypeEn} - {plateNumber}) is ready to pickup order #{assignment.Order.OrderNumber}. Open the order details to view the secure pickup code.",
                    "vendor-pickup-otp-resent",
                    assignment.OrderId,
                    $"assignmentId={assignment.Id};driverPhone={driverPhone};otpType=pickup",
                    cancellationToken);
            }
        }
        else if (otpType == "delivery")
        {
            if (assignment.IsDeliveryOtpVerified)
            {
                throw new BusinessRuleException("DELIVERY_OTP_ALREADY_VERIFIED", "Delivery OTP has already been verified.");
            }

            assignment.RegenerateDeliveryOtp(OtpTtl);
            await _context.SaveChangesAsync(cancellationToken);

            await _notificationService.SendToUserAsync(
                assignment.Order.UserId,
                "إعادة إرسال رمز التسليم",
                "Delivery OTP Resent",
                $"المندوب متواجد لتسليم طلبك رقم {assignment.Order.OrderNumber}. افتح تفاصيل الطلب لعرض رمز التسليم المؤمّن.",
                $"Driver is ready to deliver order #{assignment.Order.OrderNumber}. Open the order details to view the secure delivery code.",
                "customer-delivery-otp-resent",
                assignment.OrderId,
                $"assignmentId={assignment.Id};otpType=delivery",
                cancellationToken);

            await _oneSignalPushService.SendMobileNotificationDirectAsync(
                OneSignalMobilePushRequest.CreateHeadsUp(
                    assignment.Order.UserId.ToString(),
                    "إعادة إرسال رمز التسليم",
                    "Delivery OTP Resent",
                    $"المندوب متواجد لتسليم طلبك رقم {assignment.Order.OrderNumber}. افتح تفاصيل الطلب لعرض رمز التسليم المؤمّن.",
                    $"Driver is ready to deliver order #{assignment.Order.OrderNumber}. Open the order details to view the secure delivery code.",
                    "customer-delivery-otp-resent",
                    assignment.OrderId,
                    $"assignmentId={assignment.Id};otpType=delivery",
                    $"/orders/{assignment.OrderId}",
                    category: NotificationCategories.Order,
                    targetApplication: OneSignalApplicationTarget.Customer),
                cancellationToken);
        }
    }
}
