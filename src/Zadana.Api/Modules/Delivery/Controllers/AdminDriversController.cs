using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Api.Controllers;
using Zadana.Api.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Commands.AddDriverIncident;
using Zadana.Application.Modules.Delivery.Commands.AddDriverNote;
using Zadana.Application.Modules.Delivery.Commands.ApproveDriverDocumentReview;
using Zadana.Application.Modules.Delivery.Commands.BanDriver;
using Zadana.Application.Modules.Delivery.Commands.BlockDriverLocationUpdates;
using Zadana.Application.Modules.Delivery.Commands.ClearDriverRestrictions;
using Zadana.Application.Modules.Delivery.Commands.ReactivateDriver;
using Zadana.Application.Modules.Delivery.Commands.RejectDriverDocumentReview;
using Zadana.Application.Modules.Delivery.Commands.ReviewDriver;
using Zadana.Application.Modules.Delivery.Commands.SuspendDriver;
using Zadana.Application.Modules.Delivery.Commands.UnbanDriver;
using Zadana.Application.Modules.Delivery.Commands.UnblockDriverLocationUpdates;
using Zadana.Application.Modules.Delivery.Commands.UpdateDriverProfile;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Delivery.Controllers;

[Route("api/admin/drivers")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin - Driver Management")]
public class AdminDriversController : ApiControllerBase
{
    private readonly IDriverReadService _driverReadService;
    private readonly IApplicationDbContext _context;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly ILogger<AdminDriversController> _logger;

    public AdminDriversController(
        IDriverReadService driverReadService,
        IApplicationDbContext context,
        IIdentityAccountService identityAccountService,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        ILogger<AdminDriversController> logger)
    {
        _driverReadService = driverReadService;
        _context = context;
        _identityAccountService = identityAccountService;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetDrivers(
        [FromQuery] string? search,
        [FromQuery] string? city,
        [FromQuery] string? status,
        [FromQuery] string? verificationStatus,
        [FromQuery] string? vehicleType,
        [FromQuery] string? performance,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _driverReadService.GetAdminDriversAsync(
            search, city, status, verificationStatus, vehicleType, performance,
            Math.Max(1, page), Math.Clamp(pageSize, 1, 100), cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDriverDetail(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _driverReadService.GetAdminDriverDetailAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id:guid}/finance/entries")]
    public async Task<IActionResult> GetDriverFinanceEntries(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _driverReadService.GetAdminDriverFinanceEntriesAsync(
            id,
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, 50),
            status,
            search,
            cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    // Update driver profile from admin panel
    [HttpPut("{id:guid}/profile")]
    public async Task<IActionResult> UpdateDriverProfile(
        Guid id,
        [FromBody] UpdateDriverProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateDriverProfileCommand(
            id,
            request.FullName,
            request.Email,
            request.PhoneNumber,
            request.VehicleType,
            request.NationalId,
            request.LicenseNumber,
            request.NationalIdExpiryDate,
            request.DriverLicenseExpiryDate,
            request.VehicleLicenseNumber,
            request.VehicleLicenseExpiryDate,
            request.Address,
            request.Region,
            request.City);

        await Sender.Send(command, cancellationToken);
        return Ok(new { message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_PROFILE_UPDATED_SUCCESS"), messageAr = LocalizedMessages.GetAr("DRIVER_PROFILE_UPDATED_SUCCESS") });
    }

    [HttpPost("{id:guid}/notifications/test")]
    public async Task<ActionResult<AdminDriverNotificationResponse>> SendDriverNotification(
        Guid id,
        [FromBody] AdminSendDriverNotificationRequest? request,
        CancellationToken cancellationToken = default)
    {
        var driver = await _context.Drivers
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id, item.UserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Driver", id);

        request ??= new AdminSendDriverNotificationRequest();

        var titleAr = string.IsNullOrWhiteSpace(request.TitleAr) ? "إشعار تجريبي للمندوب" : request.TitleAr.Trim();
        var titleEn = string.IsNullOrWhiteSpace(request.TitleEn) ? "Driver test notification" : request.TitleEn.Trim();
        var bodyAr = string.IsNullOrWhiteSpace(request.BodyAr)
            ? "هذا إشعار تجريبي من واجهة المشرف للتأكد من وصول إشعارات تطبيق المندوب."
            : request.BodyAr.Trim();
        var bodyEn = string.IsNullOrWhiteSpace(request.BodyEn)
            ? "This is a test notification sent from the admin API to verify driver mobile delivery."
            : request.BodyEn.Trim();
        var type = string.IsNullOrWhiteSpace(request.Type) ? "driver_test" : request.Type.Trim();
        var eventName = string.IsNullOrWhiteSpace(request.Type) ? "account.test_notification" : type;
        var targetUrl = string.IsNullOrWhiteSpace(request.TargetUrl) ? "/notifications" : request.TargetUrl.Trim();
        var data = string.IsNullOrWhiteSpace(request.Data)
            ? DriverNotificationDataBuilder.Build(
                screen: "account_status",
                @event: eventName,
                driverId: driver.Id,
                extra: new
                {
                    source = "admin_driver_notifications_test_api",
                    userId = driver.UserId,
                    generatedAtUtc = DateTime.UtcNow,
                    targetUrl
                })
            : request.Data;

        await _notificationService.SendToUserAsync(
            driver.UserId,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            request.ReferenceId,
            data,
            cancellationToken);

        var pushRequest = OneSignalMobilePushRequest.CreateHeadsUp(
            driver.UserId.ToString(),
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            request.ReferenceId,
            data,
            targetUrl,
            category: "account",
            targetApplication: OneSignalApplicationTarget.Driver);

        if (request.SendPush)
        {
            LogPushDispatchStart(driver.Id, driver.UserId, pushRequest);
        }

        var pushResult = request.SendPush
            ? await pushRequest.DispatchAsync(_oneSignalPushService, cancellationToken)
            : new OneSignalPushDispatchResult(
                Attempted: false,
                Sent: false,
                Skipped: true,
                ProviderStatusCode: null,
                ProviderNotificationId: null,
                Reason: "Push dispatch was disabled for this admin request.");

        if (request.SendPush
            && pushResult.Skipped
            && string.Equals(pushResult.Reason, "No registered push devices found.", StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "[PUSH-DIAG] Admin driver test notification is falling back to direct OneSignal delivery without UserPushDevices registration. DriverId: {DriverId}. UserId: {UserId}. ExternalId: {ExternalId}",
                driver.Id,
                driver.UserId,
                pushRequest.ExternalUserId);

            pushResult = await _oneSignalPushService.SendMobileNotificationDirectAsync(pushRequest, cancellationToken);
        }

        if (request.SendPush)
        {
            LogPushDispatchResult(driver.Id, driver.UserId, pushRequest, pushResult);
        }

        return Ok(new AdminDriverNotificationResponse(
            Message: "Driver notification queued successfully.",
            DriverId: driver.Id,
            UserId: driver.UserId,
            ExternalId: pushRequest.ExternalUserId,
            Type: type,
            InboxRequested: true, // Cleaned automatically after the transient test offer expires.
            PushAttempted: pushResult.Attempted,
            PushSent: pushResult.Sent,
            PushSkipped: pushResult.Skipped,
            PushStatusCode: pushResult.ProviderStatusCode,
            ProviderNotificationId: pushResult.ProviderNotificationId,
            PushReason: pushResult.Reason));
    }

    [HttpPost("{id:guid}/notifications/test-offer")]
    public async Task<ActionResult<AdminDriverNotificationResponse>> SendTestDeliveryOffer(
        Guid id,
        [FromBody] AdminSendDriverTestOfferRequest? request,
        CancellationToken cancellationToken = default)
    {
        var driver = await _context.Drivers
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id, item.UserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Driver", id);

        request ??= new AdminSendDriverTestOfferRequest();

        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddSeconds(Math.Clamp(request.CountdownSeconds ?? 45, 15, 120));
        var vendorNameAr = "متجر تجريبي";
        DriverIncomingOfferDto currentOffer;
        Guid referenceOrderId;

        if (request.OrderId is Guid orderId)
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Include(item => item.Vendor)
                .Include(item => item.VendorBranch)
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken)
                ?? throw new NotFoundException("Order", orderId);

            var customerAddress = await _context.CustomerAddresses
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == order.CustomerAddressId, cancellationToken);

            var codAmount = order.PaymentMethod == Domain.Modules.Payments.Enums.PaymentMethodType.CashOnDelivery
                ? order.TotalAmount
                : 0m;
            var assignment = new DeliveryAssignment(order.Id, codAmount);
            assignment.OfferTo(driver.Id, 1, expiresAtUtc);

            currentOffer = DriverIncomingOfferFactory.Build(assignment, order, customerAddress, now);
            referenceOrderId = order.Id;
            vendorNameAr = order.Vendor?.BusinessNameAr ?? vendorNameAr;
        }
        else
        {
            referenceOrderId = Guid.NewGuid();
            currentOffer = BuildSyntheticTestOffer(referenceOrderId, expiresAtUtc, now, request.CountdownSeconds ?? 45);
        }

        var offerPayloadJson = DriverNotificationDataBuilder.BuildDispatchOfferInboxData(
            referenceOrderId,
            currentOffer.AssignmentId,
            driver.Id,
            expiresAtUtc,
            currentOffer,
            source: "admin_driver_test_offer_api");
        var pushPayloadJson = DriverNotificationDataBuilder.BuildDispatchOfferPushData(
            referenceOrderId,
            currentOffer.AssignmentId,
            driver.Id,
            expiresAtUtc,
            currentOffer,
            source: "admin_driver_test_offer_api");

        await _notificationService.SendToUserAsync(
            driver.UserId,
            new NotificationDispatchRequest(
                "عرض توصيل تجريبي",
                "Test delivery offer",
                $"لديك عرض توصيل تجريبي من {vendorNameAr} للتأكد من وصول الإشعار.",
                "You have a test delivery offer to verify mobile notification delivery.",
                NotificationTypes.DriverDeliveryOffer,
                NotificationCategories.Dispatch,
                NotificationPriorities.Critical,
                referenceOrderId,
                offerPayloadJson),
            cancellationToken);

        await _notificationService.SendDeliveryOfferToDriverAsync(
            driver.UserId,
            currentOffer,
            cancellationToken);

        var pushRequest = OneSignalMobilePushRequest.CreateHeadsUp(
            driver.UserId.ToString(),
            "عرض توصيل تجريبي",
            "Test delivery offer",
            $"لديك عرض توصيل تجريبي من {vendorNameAr} للتأكد من وصول الإشعار.",
            "You have a test delivery offer to verify mobile notification delivery.",
            NotificationTypes.DriverDeliveryOffer,
            referenceOrderId,
            pushPayloadJson,
            targetUrl: "/",
            category: NotificationCategories.Dispatch,
            targetApplication: OneSignalApplicationTarget.Driver);

        OneSignalPushDispatchResult pushResult;
        if (request.SendPush)
        {
            LogPushDispatchStart(driver.Id, driver.UserId, pushRequest);
            pushResult = await _oneSignalPushService.SendMobileNotificationDirectAsync(pushRequest, cancellationToken);
            LogPushDispatchResult(driver.Id, driver.UserId, pushRequest, pushResult);
        }
        else
        {
            pushResult = new OneSignalPushDispatchResult(
                Attempted: false,
                Sent: false,
                Skipped: true,
                ProviderStatusCode: null,
                ProviderNotificationId: null,
                Reason: "Push dispatch was disabled for this admin test offer request.");
        }

        return Ok(new AdminDriverNotificationResponse(
            Message: "Test delivery offer queued successfully.",
            DriverId: driver.Id,
            UserId: driver.UserId,
            ExternalId: pushRequest.ExternalUserId,
            Type: NotificationTypes.DriverDeliveryOffer,
            InboxRequested: true,
            PushAttempted: pushResult.Attempted,
            PushSent: pushResult.Sent,
            PushSkipped: pushResult.Skipped,
            PushStatusCode: pushResult.ProviderStatusCode,
            ProviderNotificationId: pushResult.ProviderNotificationId,
            PushReason: pushResult.Reason));
    }

    private static DriverIncomingOfferDto BuildSyntheticTestOffer(
        Guid orderId,
        DateTime expiresAtUtc,
        DateTime utcNow,
        int countdownSeconds)
    {
        var assignmentId = Guid.NewGuid();
        var resolvedCountdown = Math.Max(0, (int)(expiresAtUtc - utcNow).TotalSeconds);
        if (resolvedCountdown == 0)
        {
            resolvedCountdown = countdownSeconds;
        }

        return new DriverIncomingOfferDto(
            assignmentId,
            orderId,
            "TEST-OFFER-001",
            "Test Vendor",
            "متجر تجريبي",
            "Test Vendor",
            null,
            "Test Pickup Address, Riyadh",
            24.7137m,
            46.6754m,
            "Test Customer",
            "Test Delivery Address, Riyadh",
            24.7236m,
            46.6853m,
            1.2m,
            "12-17 min",
            15m,
            "CashOnDelivery",
            120m,
            120m,
            "TV",
            "TC",
            "Admin test delivery offer",
            resolvedCountdown,
            new[] { new DriverOfferItemDto("Test Product", 1, null) });
    }

    private void LogPushDispatchStart(
        Guid driverId,
        Guid userId,
        OneSignalMobilePushRequest pushRequest)
    {
        _logger.LogWarning(
            "[PUSH-DIAG] About to send admin driver OneSignal push. DriverId: {DriverId}. UserId: {UserId}. ExternalId: {ExternalId}. Type: {NotificationType}. ReferenceId: {ReferenceId}. TitleEn: {TitleEn}. BodyEn: {BodyEn}. Profile: {Profile}. TargetUrl: {TargetUrl}. TargetApplication: {TargetApplication}",
            driverId,
            userId,
            pushRequest.ExternalUserId,
            pushRequest.Type,
            pushRequest.ReferenceId,
            pushRequest.TitleEn,
            pushRequest.BodyEn,
            pushRequest.Profile,
            pushRequest.TargetUrl,
            pushRequest.TargetApplication);
    }

    private void LogPushDispatchResult(
        Guid driverId,
        Guid userId,
        OneSignalMobilePushRequest pushRequest,
        OneSignalPushDispatchResult pushResult)
    {
        _logger.LogWarning(
            "[PUSH-DIAG] Admin driver OneSignal push result. DriverId: {DriverId}. UserId: {UserId}. ExternalId: {ExternalId}. Type: {NotificationType}. Attempted: {Attempted}. Sent: {Sent}. Skipped: {Skipped}. StatusCode: {StatusCode}. ProviderNotificationId: {ProviderNotificationId}. Reason: {Reason}",
            driverId,
            userId,
            pushRequest.ExternalUserId,
            pushRequest.Type,
            pushResult.Attempted,
            pushResult.Sent,
            pushResult.Skipped,
            pushResult.ProviderStatusCode,
            pushResult.ProviderNotificationId,
            pushResult.Reason);
    }

    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> ReviewDriver(
        Guid id,
        [FromBody] ReviewDriverRequest request,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId
            ?? throw new UnauthorizedException("ADMIN_NOT_AUTHENTICATED");

        await Sender.Send(new ReviewDriverCommand(id, request.Action, request.Note, userId), cancellationToken);
        return Ok(new { message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_REVIEW_ACTION_APPLIED_SUCCESS"), messageAr = LocalizedMessages.GetAr("DRIVER_REVIEW_ACTION_APPLIED_SUCCESS") });
    }

    [HttpPost("{id:guid}/documents/{documentId}/approve")]
    public async Task<IActionResult> ApproveDriverDocument(
        Guid id,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        await Sender.Send(new ApproveDriverDocumentReviewCommand(id, documentId), cancellationToken);
        return Ok(new { message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_DOCUMENT_APPROVED_SUCCESS"), messageAr = LocalizedMessages.GetAr("DRIVER_DOCUMENT_APPROVED_SUCCESS") });
    }

    [HttpPost("{id:guid}/documents/{documentId}/reject")]
    public async Task<IActionResult> RejectDriverDocument(
        Guid id,
        string documentId,
        [FromBody] RejectDriverDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        await Sender.Send(new RejectDriverDocumentReviewCommand(id, documentId, request.Reason), cancellationToken);
        return Ok(new { message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_DOCUMENT_REJECTED_SUCCESS"), messageAr = LocalizedMessages.GetAr("DRIVER_DOCUMENT_REJECTED_SUCCESS") });
    }

    [HttpPost("{id:guid}/suspend")]
    public async Task<IActionResult> SuspendDriver(
        Guid id,
        [FromBody] SuspendDriverRequest? request,
        CancellationToken cancellationToken = default)
    {
        await Sender.Send(new SuspendDriverCommand(id, request?.Reason), cancellationToken);
        return Ok(new { message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_SUSPENDED_SUCCESS") });
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> ReactivateDriver(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await Sender.Send(new ReactivateDriverCommand(id), cancellationToken);
        return Ok(new { message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_REACTIVATED_SUCCESS") });
    }

    [HttpPost("{id:guid}/ban")]
    public async Task<IActionResult> BanDriver(
        Guid id,
        [FromBody] BanDriverRequest? request,
        CancellationToken cancellationToken = default)
    {
        await Sender.Send(new BanDriverCommand(id, request?.Reason), cancellationToken);
        return Ok(new { message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_BANNED_SUCCESS") });
    }

    [HttpPost("{id:guid}/unban")]
    public async Task<IActionResult> UnbanDriver(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await Sender.Send(new UnbanDriverCommand(id), cancellationToken);
        return Ok(new { message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_UNBANNED_SUCCESS") });
    }

    [HttpPost("{id:guid}/login-lock")]
    public async Task<IActionResult> LockDriverLogin(
        Guid id,
        [FromBody] DriverLoginLockRequest? request,
        CancellationToken cancellationToken = default)
    {
        var driver = await _context.Drivers.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new NotFoundException("Driver", id);

        var reason = string.IsNullOrWhiteSpace(request?.Reason)
            ? "Locked by admin"
            : request.Reason.Trim();

        var result = await _identityAccountService.LockLoginAsync(driver.UserId, reason, cancellationToken);
        if (!result.Succeeded)
        {
            throw new BusinessRuleException("DRIVER_LOGIN_LOCK_FAILED", string.Join(", ", result.Errors ?? []));
        }

        driver.Suspend(reason);
        await _context.SaveChangesAsync(cancellationToken);

        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.login_locked",
            driverId: driver.Id,
            extra: new { reason });

        await _notificationService.SendToUserAsync(
            driver.UserId,
            "قفلنا تسجيل الدخول",
            "Login locked",
            "قفلنا تسجيل دخولك. استخدم نموذج الدعم للاعتراض.",
            "Your login has been locked. Use the support form to appeal.",
            NotificationTypes.DriverAccountUpdated,
            driver.Id,
            data,
            cancellationToken);

        await _notificationService.SendDriverHomeUpdatedAsync(driver.UserId, cancellationToken);

        await _oneSignalPushService.SendMobileNotificationDirectAsync(
            OneSignalMobilePushRequest.CreateHeadsUp(
                driver.UserId.ToString(),
                "قفلنا تسجيل الدخول",
                "Login locked",
                "قفلنا تسجيل دخولك. استخدم نموذج الدعم للاعتراض.",
                "Your login has been locked. Use the support form to appeal.",
                NotificationTypes.DriverAccountUpdated,
                driver.Id,
                data,
                "/account-status",
                category: NotificationCategories.Account,
                targetApplication: OneSignalApplicationTarget.Driver),
            cancellationToken);

        return Ok(new { message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_LOGIN_LOCKED_SUCCESS"), messageAr = LocalizedMessages.GetAr("DRIVER_LOGIN_LOCKED_SUCCESS") });
    }

    [HttpPost("{id:guid}/login-unlock")]
    public async Task<IActionResult> UnlockDriverLogin(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var driver = await _context.Drivers.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new NotFoundException("Driver", id);

        var result = await _identityAccountService.UnlockLoginAsync(driver.UserId, cancellationToken);
        if (!result.Succeeded)
        {
            throw new BusinessRuleException("DRIVER_LOGIN_UNLOCK_FAILED", string.Join(", ", result.Errors ?? []));
        }

        if (driver.CanReactivate)
        {
            driver.Reactivate();
        }

        await _context.SaveChangesAsync(cancellationToken);

        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.login_unlocked",
            driverId: driver.Id);

        await _notificationService.SendToUserAsync(
            driver.UserId,
            "فتحنا تسجيل الدخول",
            "Login unlocked",
            "فتحنا تسجيل دخولك بنجاح. تقدر الآن استخدام التطبيق.",
            "Your login has been unlocked. You can now use the app.",
            NotificationTypes.DriverAccountUpdated,
            driver.Id,
            data,
            cancellationToken);

        await _notificationService.SendDriverHomeUpdatedAsync(driver.UserId, cancellationToken);

        await _oneSignalPushService.SendMobileNotificationDirectAsync(
            OneSignalMobilePushRequest.CreateHeadsUp(
                driver.UserId.ToString(),
                "فتحنا تسجيل الدخول",
                "Login unlocked",
                "فتحنا تسجيل دخولك بنجاح. تقدر الآن استخدام التطبيق.",
                "Your login has been unlocked. You can now use the app.",
                NotificationTypes.DriverAccountUpdated,
                driver.Id,
                data,
                "/account-status",
                category: NotificationCategories.Account,
                targetApplication: OneSignalApplicationTarget.Driver),
            cancellationToken);

        return Ok(new { message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_LOGIN_UNLOCKED_SUCCESS"), messageAr = LocalizedMessages.GetAr("DRIVER_LOGIN_UNLOCKED_SUCCESS") });
    }

    [HttpPost("{id:guid}/restrictions/clear")]
    public async Task<IActionResult> ClearDriverRestrictions(
        Guid id,
        [FromBody] ClearDriverRestrictionsRequest? request,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId
            ?? throw new UnauthorizedException("ADMIN_NOT_AUTHENTICATED");

        await Sender.Send(new ClearDriverRestrictionsCommand(id, userId, request?.Note), cancellationToken);
        return Ok(new { message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_RESTRICTIONS_CLEARED_SUCCESS"), messageAr = LocalizedMessages.GetAr("DRIVER_RESTRICTIONS_CLEARED_SUCCESS") });
    }

    [HttpPost("{id:guid}/location-updates/block")]
    public async Task<IActionResult> BlockLocationUpdates(
        Guid id,
        [FromBody] BlockDriverLocationUpdatesRequest? request,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId
            ?? throw new UnauthorizedException("ADMIN_NOT_AUTHENTICATED");

        await Sender.Send(
            new BlockDriverLocationUpdatesCommand(id, userId, request?.Reason),
            cancellationToken);

        return Ok(new { message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_LOCATION_UPDATES_BLOCKED_SUCCESS") });
    }

    [HttpPost("{id:guid}/location-updates/unblock")]
    public async Task<IActionResult> UnblockLocationUpdates(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await Sender.Send(new UnblockDriverLocationUpdatesCommand(id), cancellationToken);
        return Ok(new { message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_LOCATION_UPDATES_UNBLOCKED_SUCCESS") });
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> AddNote(
        Guid id,
        [FromBody] AddDriverNoteRequest request,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId
            ?? throw new UnauthorizedException("ADMIN_NOT_AUTHENTICATED");

        var noteId = await Sender.Send(
            new AddDriverNoteCommand(id, userId, request.Message), cancellationToken);

        return Ok(new { id = noteId, message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_NOTE_ADDED_SUCCESS") });
    }

    [HttpPost("{id:guid}/incidents")]
    public async Task<IActionResult> AddIncident(
        Guid id,
        [FromBody] AddDriverIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        var incidentId = await Sender.Send(
            new AddDriverIncidentCommand(
                id, request.IncidentType, request.Severity,
                request.Summary, request.LinkedOrderId, request.ReviewerName),
            cancellationToken);

        return Ok(new { id = incidentId, message = ApiLocalizedMessages.Resolve(HttpContext, "DRIVER_INCIDENT_RECORDED_SUCCESS") });
    }
}

public record ReviewDriverRequest(string Action, string? Note);
public record UpdateDriverProfileRequest(
    string FullName,
    string Email,
    string PhoneNumber,
    string? VehicleType,
    string? NationalId,
    string? LicenseNumber,
    DateTime? NationalIdExpiryDate,
    DateTime? DriverLicenseExpiryDate,
    string? VehicleLicenseNumber,
    DateTime? VehicleLicenseExpiryDate,
    string? Address,
    string? Region,
    string? City);
public record RejectDriverDocumentRequest(string Reason);
public record SuspendDriverRequest(string? Reason);
public record BanDriverRequest(string? Reason);
public record DriverLoginLockRequest(string? Reason);
public record ClearDriverRestrictionsRequest(string? Note);
public record BlockDriverLocationUpdatesRequest(string? Reason);
public record AddDriverNoteRequest(string Message);
public record AddDriverIncidentRequest(
    string IncidentType, string Severity, string Summary,
    Guid? LinkedOrderId, string? ReviewerName);
public record AdminSendDriverNotificationRequest(
    string? TitleAr = null,
    string? TitleEn = null,
    string? BodyAr = null,
    string? BodyEn = null,
    string? Type = null,
    Guid? ReferenceId = null,
    string? Data = null,
    string? TargetUrl = null,
    bool SendPush = true);
public record AdminSendDriverTestOfferRequest(
    Guid? OrderId = null,
    int? CountdownSeconds = null,
    bool SendPush = true);
public record AdminDriverNotificationResponse(
    string Message,
    Guid DriverId,
    Guid UserId,
    string ExternalId,
    string Type,
    bool InboxRequested,
    bool PushAttempted,
    bool PushSent,
    bool PushSkipped,
    int? PushStatusCode,
    string? ProviderNotificationId,
    string? PushReason);
