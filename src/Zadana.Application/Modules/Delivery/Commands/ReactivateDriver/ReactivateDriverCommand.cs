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

        await _notificationService.SendToUserAsync(
            driver.UserId,
            new NotificationDispatchRequest(
                "تمت إعادة تفعيل الحساب",
                "Driver account reactivated",
                "تمت إعادة تفعيل حسابك ويمكنك العودة للعمل.",
                "Your driver account was reactivated and you can return to work.",
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
                "تمت إعادة تفعيل الحساب",
                "Driver account reactivated",
                "تمت إعادة تفعيل حسابك ويمكنك العودة للعمل.",
                "Your driver account was reactivated and you can return to work.",
                NotificationTypes.DriverAccountUpdated,
                driver.Id,
                data,
                category: NotificationCategories.Account),
            cancellationToken);
    }
}
