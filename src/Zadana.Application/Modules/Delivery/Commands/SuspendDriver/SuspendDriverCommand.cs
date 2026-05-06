using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.SuspendDriver;

public record SuspendDriverCommand(Guid DriverId, string? Reason) : IRequest;

public class SuspendDriverCommandHandler : IRequestHandler<SuspendDriverCommand>
{
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public SuspendDriverCommandHandler(
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

    public async Task Handle(SuspendDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        driver.Suspend(request.Reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.suspend",
            driverId: driver.Id,
            extra: new
            {
                accountStatus = driver.Status.ToString(),
                reason = request.Reason
            });

        await _notificationService.SendToUserAsync(
            driver.UserId,
            new NotificationDispatchRequest(
                "تم إيقاف حساب المندوب",
                "Driver account suspended",
                "تم إيقاف حسابك مؤقتًا. راجع حالة الحساب لمعرفة التفاصيل.",
                "Your driver account was suspended. Review your account status for details.",
                NotificationTypes.DriverAccountUpdated,
                NotificationCategories.Account,
                NotificationPriorities.Critical,
                driver.Id,
                data),
            cancellationToken);

        await _notificationService.SendDriverHomeUpdatedAsync(driver.UserId, cancellationToken);

        await _oneSignalPushService.SendMobileNotificationAsync(
            OneSignalMobilePushRequest.CreateHeadsUp(
                driver.UserId.ToString(),
                "تم إيقاف حساب المندوب",
                "Driver account suspended",
                "تم إيقاف حسابك مؤقتًا. راجع حالة الحساب لمعرفة التفاصيل.",
                "Your driver account was suspended. Review your account status for details.",
                NotificationTypes.DriverAccountUpdated,
                driver.Id,
                data,
                category: NotificationCategories.Account,
                targetApplication: OneSignalApplicationTarget.Driver),
            cancellationToken);
    }
}
