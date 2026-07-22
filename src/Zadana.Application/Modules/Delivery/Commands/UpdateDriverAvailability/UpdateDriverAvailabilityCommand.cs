using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Finances.Services;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.UpdateDriverAvailability;

public record UpdateDriverAvailabilityCommand(Guid DriverUserId, bool IsAvailable) : IRequest;

public class UpdateDriverAvailabilityCommandHandler : IRequestHandler<UpdateDriverAvailabilityCommand>
{
    private readonly IDriverRepository _driverRepository;
    private readonly IDriverCommitmentPolicyService _driverCommitmentPolicyService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly DriverCodEnforcementService _driverCodEnforcementService;

    public UpdateDriverAvailabilityCommandHandler(
        IDriverRepository driverRepository,
        IDriverCommitmentPolicyService driverCommitmentPolicyService,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        DriverCodEnforcementService driverCodEnforcementService)
    {
        _driverRepository = driverRepository;
        _driverCommitmentPolicyService = driverCommitmentPolicyService;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _driverCodEnforcementService = driverCodEnforcementService;
    }

    public async Task Handle(UpdateDriverAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByUserIdAsync(request.DriverUserId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverUserId);

        if (driver.ApplyDocumentExpiryLock())
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await DriverExpiryLockNotificationDispatcher.NotifyAsync(
                driver,
                _notificationService,
                _oneSignalPushService,
                cancellationToken);
        }

        if (request.IsAvailable && !driver.CanReceiveOrders)
        {
            var message = !driver.HasServiceArea
                ? "Please choose the city you will work in before going online."
                : "Your account must be approved before you can go online.";

            throw new BusinessRuleException(
                "DRIVER_NOT_READY_FOR_DISPATCH",
                message);
        }

        if (request.IsAvailable && driver.IsLocationUpdatesBlocked)
        {
            throw new BusinessRuleException(
                "DRIVER_LOCATION_UPDATES_BLOCKED",
                "Location updates are blocked for this account, so availability cannot be enabled.");
        }

        if (request.IsAvailable)
        {
            var commitmentSummary = await _driverCommitmentPolicyService.GetDriverSummaryAsync(driver.Id, cancellationToken);
            if (!commitmentSummary.CanReceiveOffers)
            {
                throw new BusinessRuleException(
                    "DRIVER_SOFT_BLOCKED_BY_REJECTIONS",
                    commitmentSummary.RestrictionMessage ??
                    "You have exceeded the offer rejection limit. Please try again later.");
            }

            if (await _driverCodEnforcementService.IsDriverBlockedAsync(driver.Id, cancellationToken))
            {
                var codOwed = await _driverCodEnforcementService.GetCodOwedBalanceAsync(driver.Id, cancellationToken);
                throw new BusinessRuleException(
                    "DRIVER_COD_BLOCKED",
                    DriverOperationalStatusFactory.ResolveCodRestrictionMessageEn(codOwed));
            }
        }

        driver.ToggleAvailability(request.IsAvailable);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
