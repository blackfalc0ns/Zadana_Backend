using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.ApproveDriverDocumentReview;

public record ApproveDriverDocumentReviewCommand(Guid DriverId, string DocumentId) : IRequest;

public class ApproveDriverDocumentReviewCommandHandler : IRequestHandler<ApproveDriverDocumentReviewCommand>
{
    private readonly IDriverRepository _driverRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public ApproveDriverDocumentReviewCommandHandler(
        IDriverRepository driverRepository,
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IIdentityAccountService identityAccountService,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService)
    {
        _driverRepository = driverRepository;
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _identityAccountService = identityAccountService;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
    }

    public async Task Handle(ApproveDriverDocumentReviewCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByIdWithReviewsAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        var documentType = ParseDocumentType(request.DocumentId);
        if (!CanReview(driver, documentType))
        {
            throw new BusinessRuleException("DRIVER_DOCUMENT_NOT_REVIEWABLE", "Driver document is not uploaded or is incomplete.");
        }

        var reviewerName = await ResolveReviewerNameAsync(cancellationToken);
        driver.GetOrCreateDocumentReview(documentType).Approve(_currentUserService.UserId, reviewerName);

        if (DriverProfileReadinessFactory.GetMissingRequirements(driver, driver.User).Count == 0 &&
            DriverProfileReadinessFactory.AreRequiredDocumentsApproved(driver))
        {
            driver.RefreshProfileReviewState(true, sensitiveChange: true, note: "Documents approved and pending final account approval");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var documentNameAr = GetDocumentNameAr(documentType);
        var documentNameEn = GetDocumentNameEn(documentType);
        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.document_approved",
            driverId: driver.Id,
            extra: new
            {
                documentType = documentType.ToString(),
                documentId = request.DocumentId,
                verificationStatus = driver.VerificationStatus.ToString(),
                accountStatus = driver.Status.ToString()
            });

        await _notificationService.SendToUserAsync(
            driver.UserId,
            new NotificationDispatchRequest(
                $"تمت الموافقة على {documentNameAr}",
                $"{documentNameEn} approved",
                $"تمت مراجعة {documentNameAr} والموافقة عليه. يمكنك متابعة حالة حسابك من التطبيق.",
                $"Your {documentNameEn.ToLowerInvariant()} was reviewed and approved. You can track your account status in the app.",
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
                $"تمت الموافقة على {documentNameAr}",
                $"{documentNameEn} approved",
                $"تمت مراجعة {documentNameAr} والموافقة عليه. يمكنك متابعة حالة حسابك من التطبيق.",
                $"Your {documentNameEn.ToLowerInvariant()} was reviewed and approved. You can track your account status in the app.",
                NotificationTypes.DriverAccountUpdated,
                driver.Id,
                data,
                targetUrl: "/account-status",
                category: NotificationCategories.Account,
                targetApplication: OneSignalApplicationTarget.Driver),
            cancellationToken);
    }

    private static DriverDocumentType ParseDocumentType(string documentId) =>
        Enum.TryParse<DriverDocumentType>(documentId, true, out var parsed)
            ? parsed
            : throw new NotFoundException("DriverDocument", documentId);

    private static bool CanReview(Domain.Modules.Delivery.Entities.Driver driver, DriverDocumentType type) =>
        type switch
        {
            DriverDocumentType.NationalId => DriverProfileReadinessFactory.HasNationalIdPacket(driver),
            DriverDocumentType.DriverLicense => DriverProfileReadinessFactory.HasDriverLicensePacket(driver),
            DriverDocumentType.VehicleLicense => DriverProfileReadinessFactory.HasVehicleLicensePacket(driver),
            _ => false
        };

    private async Task<string> ResolveReviewerNameAsync(CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return "Driver Compliance Desk";
        }

        var actor = await _identityAccountService.FindByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        return string.IsNullOrWhiteSpace(actor?.FullName) ? "Driver Compliance Desk" : actor.FullName;
    }

    private static string GetDocumentNameAr(DriverDocumentType type) =>
        type switch
        {
            DriverDocumentType.NationalId => "البطاقة الشخصية",
            DriverDocumentType.DriverLicense => "رخصة القيادة",
            DriverDocumentType.VehicleLicense => "رخصة المركبة",
            _ => "المستند"
        };

    private static string GetDocumentNameEn(DriverDocumentType type) =>
        type switch
        {
            DriverDocumentType.NationalId => "National ID",
            DriverDocumentType.DriverLicense => "Driver license",
            DriverDocumentType.VehicleLicense => "Vehicle license",
            _ => "Document"
        };
}
