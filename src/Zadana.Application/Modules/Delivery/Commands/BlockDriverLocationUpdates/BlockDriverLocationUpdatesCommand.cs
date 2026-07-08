using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.BlockDriverLocationUpdates;

public record BlockDriverLocationUpdatesCommand(Guid DriverId, Guid AdminUserId, string? Reason) : IRequest;

public class BlockDriverLocationUpdatesCommandHandler : IRequestHandler<BlockDriverLocationUpdatesCommand>
{
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public BlockDriverLocationUpdatesCommandHandler(
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

    public async Task Handle(BlockDriverLocationUpdatesCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        driver.BlockLocationUpdates(request.AdminUserId, request.Reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.location_blocked",
            driverId: driver.Id,
            extra: new
            {
                reason = request.Reason,
                locationUpdatesBlocked = driver.IsLocationUpdatesBlocked
            });

        const string titleAr = "أوقفنا تحديثات الموقع";
        const string titleEn = "Location updates blocked";
        const string bodyAr = "أوقفنا تحديثات موقعك مؤقتًا من فريق التشغيل.";
        const string bodyEn = "Your location updates were temporarily blocked by the team.";

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
