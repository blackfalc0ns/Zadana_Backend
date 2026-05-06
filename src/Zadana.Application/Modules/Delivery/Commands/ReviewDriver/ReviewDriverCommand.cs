using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.ReviewDriver;

public record ReviewDriverCommand(
    Guid DriverId,
    string Action, // "approve" | "request-docs" | "reject"
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

    public ReviewDriverCommandHandler(
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

    public async Task Handle(ReviewDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        switch (request.Action.ToLowerInvariant())
        {
            case "approve":
                driver.Approve(request.ReviewerUserId, request.Note);
                break;
            case "request-docs":
                driver.RequestDocuments(request.ReviewerUserId, request.Note);
                break;
            case "reject":
                driver.Reject(request.ReviewerUserId, request.Note);
                break;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var (eventName, titleAr, titleEn, bodyAr, bodyEn) = request.Action.ToLowerInvariant() switch
        {
            "approve" => (
                "account.approve",
                "تم اعتماد حساب المندوب",
                "Driver account approved",
                "تمت مراجعة حسابك واعتماده. يمكنك الآن متابعة العمل من التطبيق.",
                "Your driver account was approved. You can continue working from the app."),
            "request-docs" => (
                "account.request_docs",
                "مطلوب استكمال المستندات",
                "Additional documents required",
                "يرجى مراجعة حسابك واستكمال المستندات المطلوبة.",
                "Please review your account and upload the required documents."),
            _ => (
                "account.reject",
                "تم رفض طلب التسجيل",
                "Driver application rejected",
                "تم رفض طلب التسجيل الحالي. راجع ملاحظات الفريق داخل التطبيق.",
                "Your current driver application was rejected. Review the team note in the app.")
        };

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
                category: NotificationCategories.Account,
                targetApplication: OneSignalApplicationTarget.Driver),
            cancellationToken);
    }
}
