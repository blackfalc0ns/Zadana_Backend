using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.UnbanDriver;

public record UnbanDriverCommand(Guid DriverId) : IRequest;

public class UnbanDriverCommandHandler : IRequestHandler<UnbanDriverCommand>
{
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public UnbanDriverCommandHandler(
        IDriverRepository driverRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService)
    {
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
    }

    public async Task Handle(UnbanDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        if (driver.ApplyDocumentExpiryLock())
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new BusinessRuleException(
                "DRIVER_DOCUMENTS_EXPIRED",
                "Cannot unban driver account while required documents are expired.");
        }

        if (!driver.CanReactivate)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_ELIGIBLE_FOR_UNBAN",
                "Driver account must be approved and have valid required documents before unban.");
        }

        driver.Reactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.unban",
            driverId: driver.Id,
            extra: new
            {
                accountStatus = driver.Status.ToString(),
                verificationStatus = driver.VerificationStatus.ToString()
            });

        const string titleAr = "تم فك حظر حساب المندوب";
        const string titleEn = "Driver account unbanned";
        const string bodyAr = "تم فك حظر حسابك كمندوب. يمكنك العودة إلى وضع الاستعداد عند توفر شروط التشغيل.";
        const string bodyEn = "Your driver account ban was lifted. You can return to standby mode when operating requirements are met.";

        await _notificationService.SendToUserAsync(
            driver.UserId,
            new NotificationDispatchRequest(
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                NotificationTypes.DriverAccountUpdated,
                NotificationCategories.Account,
                NotificationPriorities.High,
                driver.Id,
                data),
            cancellationToken);

        await _notificationService.SendDriverHomeUpdatedAsync(driver.UserId, cancellationToken);

        await _oneSignalPushService.SendMobileNotificationAsync(
            OneSignalMobilePushRequest.CreateHeadsUp(
                driver.UserId.ToString(),
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                NotificationTypes.DriverAccountUpdated,
                driver.Id,
                data,
                targetUrl: "/account-status",
                category: NotificationCategories.Account,
                targetApplication: OneSignalApplicationTarget.Driver),
            cancellationToken);
    }
}
