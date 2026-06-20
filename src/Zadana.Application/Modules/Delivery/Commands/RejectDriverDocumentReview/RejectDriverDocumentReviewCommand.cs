using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Exceptions;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.RejectDriverDocumentReview;

public record RejectDriverDocumentReviewCommand(Guid DriverId, string DocumentId, string Reason) : IRequest;

public class RejectDriverDocumentReviewCommandHandler : IRequestHandler<RejectDriverDocumentReviewCommand>
{
    private readonly IDriverRepository _driverRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly ILogger<RejectDriverDocumentReviewCommandHandler> _logger;

    public RejectDriverDocumentReviewCommandHandler(
        IDriverRepository driverRepository,
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IIdentityAccountService identityAccountService,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        ILogger<RejectDriverDocumentReviewCommandHandler> logger)
    {
        _driverRepository = driverRepository;
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _identityAccountService = identityAccountService;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _logger = logger;
    }

    public async Task Handle(RejectDriverDocumentReviewCommand request, CancellationToken cancellationToken)
    {
        await ApplyRejectionAsync(request, cancellationToken, allowRetry: true);
    }

    private async Task ApplyRejectionAsync(
        RejectDriverDocumentReviewCommand request,
        CancellationToken cancellationToken,
        bool allowRetry)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure("Reason", "A rejection reason is required.")
            });
        }

        var driver = await _driverRepository.GetByIdWithReviewsAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        var documentType = ParseDocumentType(request.DocumentId);
        if (!CanReview(driver, documentType))
        {
            throw new BusinessRuleException("DRIVER_DOCUMENT_NOT_REVIEWABLE", "The selected driver document is missing or incomplete.");
        }

        var reviewerName = await ResolveReviewerNameAsync(cancellationToken);
        var existingReview = driver.DocumentReviews.FirstOrDefault(item => item.Type == documentType);
        var documentReview = existingReview ?? driver.GetOrCreateDocumentReview(documentType);
        if (existingReview is null)
        {
            _dbContext.DriverDocumentReviews?.Add(documentReview);
        }

        documentReview.Reject(request.Reason, _currentUserService.UserId, reviewerName);
        driver.RequestDocuments(_currentUserService.UserId ?? Guid.Empty, request.Reason);

        DetachDriverUserIfTracked(driver);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex) when (allowRetry && _dbContext is DbContext efContext)
        {
            _logger.LogWarning(ex, "Driver document rejection hit a concurrency conflict for driver {DriverId}. Retrying once.", request.DriverId);
            efContext.ChangeTracker.Clear();
            await ApplyRejectionAsync(request, cancellationToken, allowRetry: false);
            return;
        }

        var documentNameAr = GetDocumentNameAr(documentType);
        var documentNameEn = GetDocumentNameEn(documentType);
        var titleAr = $"مطلوب تعديل {documentNameAr}";
        var titleEn = $"{documentNameEn} needs correction";
        var bodyAr = $"تمت مراجعة {documentNameAr} ويوجد نقص أو خطأ. السبب: {request.Reason}";
        var bodyEn = $"Your {documentNameEn.ToLowerInvariant()} needs correction. Reason: {request.Reason}";
        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.document_rejected",
            driverId: driver.Id,
            titleAr: titleAr,
            titleEn: titleEn,
            bodyAr: bodyAr,
            bodyEn: bodyEn,
            extra: new
            {
                documentType = documentType.ToString(),
                documentId = request.DocumentId,
                verificationStatus = driver.VerificationStatus.ToString(),
                accountStatus = driver.Status.ToString(),
                reason = request.Reason
            });

        await NotifyDriverAsync(driver, titleAr, titleEn, bodyAr, bodyEn, data, cancellationToken);
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
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string data,
        CancellationToken cancellationToken)
    {
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
                    NotificationPriorities.Critical,
                    driver.Id,
                    data),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver document rejection inbox notification failed for driver {DriverId}", driver.Id);
        }

        try
        {
            await _notificationService.SendDriverHomeUpdatedAsync(driver.UserId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver home refresh notification failed after rejecting document for driver {DriverId}", driver.Id);
        }

        try
        {
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver document rejection push notification failed for driver {DriverId}", driver.Id);
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
