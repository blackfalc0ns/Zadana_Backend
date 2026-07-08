using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.ClearDriverRestrictions;

public record ClearDriverRestrictionsCommand(Guid DriverId, Guid AdminUserId, string? Note) : IRequest;

public class ClearDriverRestrictionsCommandHandler : IRequestHandler<ClearDriverRestrictionsCommand>
{
    private const string OfferComplianceIncidentType = "offer-compliance";

    private readonly IDriverRepository _driverRepository;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public ClearDriverRestrictionsCommandHandler(
        IDriverRepository driverRepository,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService)
    {
        _driverRepository = driverRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
    }

    public async Task Handle(ClearDriverRestrictionsCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        if (driver.HasExpiredRequiredDocuments())
        {
            throw new BusinessRuleException(
                "DRIVER_DOCUMENTS_EXPIRED",
                "Cannot clear driver restrictions while required documents are expired.");
        }

        if (driver.VerificationStatus != DriverVerificationStatus.Approved)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_APPROVED",
                "Cannot clear driver restrictions before the driver account is approved.");
        }

        driver.ClearOperationalRestrictions(request.AdminUserId, request.Note);

        var openOfferComplianceIncidents = await _context.DriverIncidents
            .Where(incident =>
                incident.DriverId == driver.Id &&
                incident.IncidentType == OfferComplianceIncidentType &&
                incident.Status != DriverIncidentStatus.Resolved)
            .ToListAsync(cancellationToken);

        foreach (var incident in openOfferComplianceIncidents)
        {
            incident.Resolve();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.restrictions_cleared",
            driverId: driver.Id,
            extra: new
            {
                accountStatus = driver.Status.ToString(),
                locationUpdatesBlocked = driver.IsLocationUpdatesBlocked,
                commitmentClearedAtUtc = driver.CommitmentClearedAtUtc,
                canReceiveOffers = true,
                isFrozen = false,
                note = request.Note
            });

        const string titleAr = "فكّينا قيود المندوب";
        const string titleEn = "Driver restrictions cleared";
        const string bodyAr = "فكّينا القيود التشغيلية على حسابك. تقدر تستقبل العروض مرة أخرى بعد تفعيل حالة التوفر.";
        const string bodyEn = "Your operational restrictions were cleared. You can receive offers again after going available.";

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
