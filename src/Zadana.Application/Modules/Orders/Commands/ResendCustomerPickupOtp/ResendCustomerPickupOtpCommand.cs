using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.ResendCustomerPickupOtp;

public record ResendCustomerPickupOtpCommand(
    Guid OrderId,
    Guid UserId) : IRequest<ResendCustomerPickupOtpResultDto>;

public record ResendCustomerPickupOtpResultDto(Guid OrderId, DateTime ExpiresAtUtc, string Message);

public class ResendCustomerPickupOtpCommandValidator : AbstractValidator<ResendCustomerPickupOtpCommand>
{
    public ResendCustomerPickupOtpCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class ResendCustomerPickupOtpCommandHandler : IRequestHandler<ResendCustomerPickupOtpCommand, ResendCustomerPickupOtpResultDto>
{
    private const int DefaultMaxResendsPerHour = 3;

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public ResendCustomerPickupOtpCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
    }

    public async Task<ResendCustomerPickupOtpResultDto> Handle(
        ResendCustomerPickupOtpCommand request,
        CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(item => item.Id == request.OrderId && item.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        var settings = await PlatformPickupSettingsSupport.LoadAsync(_context, cancellationToken);
        var otpTtl = PlatformPickupSettingsSupport.ResolveOtpTtl(settings);

        order.RegenerateCustomerPickupOtp(otpTtl, DefaultMaxResendsPerHour);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var expiresAtUtc = order.PickupOtpExpiresAtUtc
            ?? throw new BusinessRuleException("PICKUP_OTP_NOT_READY", "Pickup OTP expiry could not be determined.");

        await DispatchPickupOtpRegeneratedNotificationAsync(order, cancellationToken);

        return new ResendCustomerPickupOtpResultDto(
            order.Id,
            expiresAtUtc,
            "Pickup OTP regenerated. Open order details to view the new code.");
    }

    private async Task DispatchPickupOtpRegeneratedNotificationAsync(
        Domain.Modules.Orders.Entities.Order order,
        CancellationToken cancellationToken)
    {
        const string notificationType = NotificationTypes.PickupOtpRegenerated;
        var targetUrl = OrderStatusNotificationComposer.ResolveTargetUrl(order.Id);

        await _notificationService.SendToUserAsync(
            order.UserId,
            "تم إعادة إرسال رمز الاستلام",
            "Pickup OTP Regenerated",
            $"تم إنشاء رمز استلام جديد لطلبك رقم {order.OrderNumber}. افتح تفاصيل الطلب لعرض الرمز.",
            $"A new pickup code was generated for order #{order.OrderNumber}. Open order details to view the code.",
            notificationType,
            order.Id,
            $"orderId={order.Id};otpType=customer_pickup",
            cancellationToken);

        await _oneSignalPushService.SendMobileNotificationDirectAsync(
            OneSignalMobilePushRequest.CreateHeadsUp(
                order.UserId.ToString(),
                "تم إعادة إرسال رمز الاستلام",
                "Pickup OTP Regenerated",
                $"تم إنشاء رمز استلام جديد لطلبك رقم {order.OrderNumber}. افتح تفاصيل الطلب لعرض الرمز.",
                $"A new pickup code was generated for order #{order.OrderNumber}. Open order details to view the code.",
                notificationType,
                order.Id,
                $"orderId={order.Id};otpType=customer_pickup",
                targetUrl,
                category: NotificationCategories.Order,
                targetApplication: OneSignalApplicationTarget.Customer),
            cancellationToken);
    }
}
