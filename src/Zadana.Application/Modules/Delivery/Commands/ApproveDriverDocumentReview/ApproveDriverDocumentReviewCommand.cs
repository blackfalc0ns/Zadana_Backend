using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<ApproveDriverDocumentReviewCommandHandler> _logger;

    public ApproveDriverDocumentReviewCommandHandler(
        IDriverRepository driverRepository,
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IIdentityAccountService identityAccountService,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        ILogger<ApproveDriverDocumentReviewCommandHandler> logger)
    {
        _driverRepository = driverRepository;
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _identityAccountService = identityAccountService;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _logger = logger;
    }

    public async Task Handle(ApproveDriverDocumentReviewCommand request, CancellationToken cancellationToken)
    {
        await ApplyApprovalAsync(request, cancellationToken, allowRetry: true);
    }

    private async Task ApplyApprovalAsync(
        ApproveDriverDocumentReviewCommand request,
        CancellationToken cancellationToken,
        bool allowRetry)
    {
        var driver = await _driverRepository.GetByIdWithReviewsAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        var documentType = ParseDocumentType(request.DocumentId);
        if (!CanReview(driver, documentType))
        {
            throw new BusinessRuleException("DRIVER_DOCUMENT_NOT_REVIEWABLE", "The selected driver document is missing or incomplete.");
        }

        if (IsExpired(driver, documentType))
        {
            throw new BusinessRuleException("DRIVER_DOCUMENT_EXPIRED", "This document is expired and cannot be approved until the driver uploads a renewed version.");
        }

        var reviewerName = await ResolveReviewerNameAsync(cancellationToken);
        var existingReview = driver.DocumentReviews.FirstOrDefault(item => item.Type == documentType);
        var documentReview = existingReview ?? driver.GetOrCreateDocumentReview(documentType);
        if (existingReview is null)
        {
            _dbContext.DriverDocumentReviews?.Add(documentReview);
        }

        documentReview.Approve(_currentUserService.UserId, reviewerName);

        if (driver.User is not null &&
            DriverProfileReadinessFactory.GetMissingRequirements(driver, driver.User).Count == 0 &&
            DriverProfileReadinessFactory.AreRequiredDocumentsApproved(driver))
        {
            driver.RefreshProfileReviewState(true, sensitiveChange: true, note: "Documents approved and pending final account approval");
        }

        DetachDriverUserIfTracked(driver);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex) when (allowRetry && _dbContext is DbContext efContext)
        {
            _logger.LogWarning(ex, "Driver document approval hit a concurrency conflict for driver {DriverId}. Retrying once.", request.DriverId);
            efContext.ChangeTracker.Clear();
            await ApplyApprovalAsync(request, cancellationToken, allowRetry: false);
            return;
        }

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

        await NotifyDriverAsync(driver, documentNameAr, documentNameEn, data, cancellationToken);
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

    private static bool IsExpired(Domain.Modules.Delivery.Entities.Driver driver, DriverDocumentType type)
    {
        var expiryDate = type switch
        {
            DriverDocumentType.NationalId => driver.NationalIdExpiryDate,
            DriverDocumentType.DriverLicense => driver.DriverLicenseExpiryDate,
            DriverDocumentType.VehicleLicense => driver.VehicleLicenseExpiryDate,
            _ => null
        };

        return expiryDate.HasValue && expiryDate.Value.Date < DateTime.UtcNow.Date;
    }

    private void DetachDriverUserIfTracked(Domain.Modules.Delivery.Entities.Driver driver)
    {
        if (_dbContext is not DbContext efContext || driver.User is null)
        {
            return;
        }

        var userEntry = efContext.Entry(driver.User);
        if (userEntry.State != EntityState.Detached)
        {
            userEntry.State = EntityState.Detached;
        }
    }

    private async Task<string> ResolveReviewerNameAsync(CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return "Driver Compliance Desk";
        }

        var actor = await _identityAccountService.FindByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        return string.IsNullOrWhiteSpace(actor?.FullName) ? "Driver Compliance Desk" : actor.FullName;
    }

    private async Task NotifyDriverAsync(
        Domain.Modules.Delivery.Entities.Driver driver,
        string documentNameAr,
        string documentNameEn,
        string data,
        CancellationToken cancellationToken)
    {
        var titleAr = $"تمت الموافقة على {documentNameAr}";
        var titleEn = $"{documentNameEn} approved";
        var bodyAr = $"تمت مراجعة {documentNameAr} والموافقة عليه. يمكنك متابعة حالة حسابك من التطبيق.";
        var bodyEn = $"Your {documentNameEn.ToLowerInvariant()} was reviewed and approved. You can track your account status in the app.";

        try
        {
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
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver document approval inbox notification failed for driver {DriverId}", driver.Id);
        }

        try
        {
            await _notificationService.SendDriverHomeUpdatedAsync(driver.UserId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver home refresh notification failed after approving document for driver {DriverId}", driver.Id);
        }

        try
        {
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver document approval push notification failed for driver {DriverId}", driver.Id);
        }
    }

    private static string GetDocumentNameAr(DriverDocumentType type) =>
        type switch
        {
            DriverDocumentType.NationalId => "الهوية الوطنية",
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
