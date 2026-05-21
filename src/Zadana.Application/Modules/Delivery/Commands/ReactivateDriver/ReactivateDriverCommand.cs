using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.ReactivateDriver;

public record ReactivateDriverCommand(Guid DriverId) : IRequest;

public class ReactivateDriverCommandHandler : IRequestHandler<ReactivateDriverCommand>
{
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public ReactivateDriverCommandHandler(
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

    public async Task Handle(ReactivateDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        if (driver.ApplyDocumentExpiryLock())
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new BusinessRuleException(
                "DRIVER_DOCUMENTS_EXPIRED",
                "Cannot reactivate driver account while required documents are expired.");
        }

        if (!driver.CanReactivate)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_ELIGIBLE_FOR_REACTIVATION",
                "Driver account must be approved and have valid required documents before reactivation.");
        }

        driver.Reactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.reactivate",
            driverId: driver.Id,
            extra: new
            {
                accountStatus = driver.Status.ToString(),
                verificationStatus = driver.VerificationStatus.ToString()
            });

        const string titleAr = "تمت إعادة تفعيل حساب المندوب";
        const string titleEn = "Driver account reactivated";
        const string bodyAr = "تمت إعادة تفعيل حسابك كمندوب ويمكنك العودة للعمل.";
        const string bodyEn = "Your driver account was reactivated and you can return to work.";

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
            OneSignalMobilePushRequest.CreateStandard(
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
