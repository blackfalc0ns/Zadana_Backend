using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.ReviewDriver;

public record ReviewDriverCommand(
    Guid DriverId,
    string Action,
    string? Note,
    Guid ReviewerUserId) : IRequest;

public class ReviewDriverCommandValidator : AbstractValidator<ReviewDriverCommand>
{
    private static readonly string[] AllowedActions = ["approve", "request-docs", "reject"];

    public ReviewDriverCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.DriverId).NotEmpty();
        RuleFor(x => x.ReviewerUserId).NotEmpty();
        RuleFor(x => x.Action)
            .NotEmpty()
            .Must(a => AllowedActions.Contains(a.ToLowerInvariant()))
            .WithMessage("Action must be: approve, request-docs, or reject");
    }
}

public class ReviewDriverCommandHandler : IRequestHandler<ReviewDriverCommand>
{
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly ILogger<ReviewDriverCommandHandler> _logger;

    public ReviewDriverCommandHandler(
        IDriverRepository driverRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        ILogger<ReviewDriverCommandHandler> logger)
    {
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _logger = logger;
    }

    public async Task Handle(ReviewDriverCommand request, CancellationToken cancellationToken)
    {
        await ApplyReviewAsync(request, cancellationToken, allowRetry: true);
    }

    private async Task ApplyReviewAsync(
        ReviewDriverCommand request,
        CancellationToken cancellationToken,
        bool allowRetry)
    {
        var driver = await _driverRepository.GetByIdWithReviewsAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        switch (request.Action.ToLowerInvariant())
        {
            case "approve":
                var missingRequirements = DriverProfileReadinessFactory.GetMissingRequirements(driver, driver.User);
                if (missingRequirements.Count > 0 || !DriverProfileReadinessFactory.AreRequiredDocumentsApproved(driver))
                {
                    throw new BusinessRuleException("DRIVER_DOCUMENTS_NOT_APPROVED", "All required driver documents must be approved before final account approval.");
                }

                driver.Approve(request.ReviewerUserId, request.Note);
                break;
            case "request-docs":
                driver.RequestDocuments(request.ReviewerUserId, request.Note);
                break;
            case "reject":
                driver.Reject(request.ReviewerUserId, request.Note);
                break;
        }

        DetachDriverUserIfTracked(driver);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex) when (allowRetry && _unitOfWork is DbContext efContext)
        {
            _logger.LogWarning(ex, "Driver review hit a concurrency conflict for driver {DriverId}. Retrying once.", request.DriverId);
            efContext.ChangeTracker.Clear();
            await ApplyReviewAsync(request, cancellationToken, allowRetry: false);
            return;
        }

        var (eventName, titleAr, titleEn, bodyAr, bodyEn) = BuildReviewNotification(request.Action);
        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: eventName,
            driverId: driver.Id,
            extra: new
            {
                verificationStatus = driver.VerificationStatus.ToString(),
                accountStatus = driver.Status.ToString(),
                note = request.Note
            });

        await NotifyDriverAsync(driver.UserId, driver.Id, titleAr, titleEn, bodyAr, bodyEn, data, cancellationToken);
    }

    private async Task NotifyDriverAsync(
        Guid driverUserId,
        Guid driverId,
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
                driverUserId,
                new NotificationDispatchRequest(
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    NotificationTypes.DriverAccountUpdated,
                    NotificationCategories.Account,
                    NotificationPriorities.High,
                    driverId,
                    data),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver review inbox notification failed for driver {DriverId}", driverId);
        }

        try
        {
            await _notificationService.SendDriverHomeUpdatedAsync(driverUserId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver home refresh notification failed after review action for driver {DriverId}", driverId);
        }

        try
        {
            await _oneSignalPushService.SendMobileNotificationAsync(
                OneSignalMobilePushRequest.CreateStandard(
                    driverUserId.ToString(),
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    NotificationTypes.DriverAccountUpdated,
                    driverId,
                    data,
                    category: NotificationCategories.Account,
                    targetApplication: OneSignalApplicationTarget.Driver),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver review push notification failed for driver {DriverId}", driverId);
        }
    }

    private static (string EventName, string TitleAr, string TitleEn, string BodyAr, string BodyEn) BuildReviewNotification(string action) =>
        action.ToLowerInvariant() switch
        {
            "approve" => (
                "account.approve",
                "Driver account approved",
                "Driver account approved",
                "Your driver account was approved. You can continue working from the app.",
                "Your driver account was approved. You can continue working from the app."),
            "request-docs" => (
                "account.request_docs",
                "Additional documents required",
                "Additional documents required",
                "Please review your account and upload the required documents.",
                "Please review your account and upload the required documents."),
            _ => (
                "account.reject",
                "Driver application rejected",
                "Driver application rejected",
                "Your current driver application was rejected. Review the team note in the app.",
                "Your current driver application was rejected. Review the team note in the app.")
        };

    private void DetachDriverUserIfTracked(Domain.Modules.Delivery.Entities.Driver driver)
    {
        if (_unitOfWork is not DbContext efContext || driver.User is null)
        {
            return;
        }

        var userEntry = efContext.Entry(driver.User);
        if (userEntry.State != EntityState.Detached)
        {
            userEntry.State = EntityState.Detached;
        }
    }
}
