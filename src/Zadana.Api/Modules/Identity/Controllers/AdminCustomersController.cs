using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Queries.AdminCustomers;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/admin/customers")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin Dashboard API")]
public class AdminCustomersController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly ILogger<AdminCustomersController> _logger;

    public AdminCustomersController(
        IApplicationDbContext context,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        ILogger<AdminCustomersController> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await Sender.Send(new GetAdminCustomersQuery(search, page, pageSize));
        return Ok(result);
    }

    [HttpGet("{customerId:guid}")]
    public async Task<IActionResult> GetCustomerDetail(Guid customerId)
    {
        var result = await Sender.Send(new GetAdminCustomerDetailQuery(customerId));
        return Ok(result);
    }

    [HttpPost("{customerId:guid}/notifications/test")]
    public async Task<ActionResult<AdminCustomerNotificationResponse>> SendCustomerNotification(
        Guid customerId,
        [FromBody] AdminSendCustomerNotificationRequest? request,
        CancellationToken cancellationToken = default)
    {
        var customer = await _context.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.Customer && user.Id == customerId)
            .Select(user => new { user.Id })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Customer", customerId);

        request ??= new AdminSendCustomerNotificationRequest();

        var titleAr = string.IsNullOrWhiteSpace(request.TitleAr) ? "إشعار تجريبي للعميل" : request.TitleAr.Trim();
        var titleEn = string.IsNullOrWhiteSpace(request.TitleEn) ? "Customer test notification" : request.TitleEn.Trim();
        var bodyAr = string.IsNullOrWhiteSpace(request.BodyAr)
            ? "هذا إشعار تجريبي من واجهة الأدمن للتأكد من وصول إشعارات الموبايل للعميل."
            : request.BodyAr.Trim();
        var bodyEn = string.IsNullOrWhiteSpace(request.BodyEn)
            ? "This is a test notification sent from the admin API to verify customer mobile delivery."
            : request.BodyEn.Trim();
        var type = string.IsNullOrWhiteSpace(request.Type) ? "customer_test" : request.Type.Trim();
        var targetUrl = string.IsNullOrWhiteSpace(request.TargetUrl) ? "/notifications" : request.TargetUrl.Trim();
        var data = string.IsNullOrWhiteSpace(request.Data)
            ? JsonSerializer.Serialize(new
            {
                source = "admin_customer_notifications_test_api",
                customerId = customer.Id,
                userId = customer.Id,
                generatedAtUtc = DateTime.UtcNow,
                targetUrl
            })
            : request.Data;

        await _notificationService.SendToUserAsync(
            customer.Id,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            request.ReferenceId,
            data,
            cancellationToken);
        var pushRequest = OneSignalMobilePushRequest.CreateHeadsUp(
            customer.Id.ToString(),
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            request.ReferenceId,
            data,
            targetUrl);

        if (request.SendPush)
        {
            LogPushDispatchStart(customer.Id, pushRequest);
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

        if (request.SendPush)
        {
            LogPushDispatchResult(customer.Id, pushRequest, pushResult);
        }

        return Ok(new AdminCustomerNotificationResponse(
            Message: "Customer notification queued successfully.",
            CustomerId: customer.Id,
            UserId: customer.Id,
            ExternalId: customer.Id.ToString(),
            Type: type,
            InboxRequested: true,
            PushAttempted: pushResult.Attempted,
            PushSent: pushResult.Sent,
            PushSkipped: pushResult.Skipped,
            PushStatusCode: pushResult.ProviderStatusCode,
            ProviderNotificationId: pushResult.ProviderNotificationId,
            PushReason: pushResult.Reason));
    }

    private void LogPushDispatchStart(Guid customerId, OneSignalMobilePushRequest pushRequest)
    {
        _logger.LogWarning(
            "[PUSH-DIAG] About to send admin customer OneSignal push. CustomerId: {CustomerId}. ExternalId: {ExternalId}. Type: {NotificationType}. ReferenceId: {ReferenceId}. TitleEn: {TitleEn}. BodyEn: {BodyEn}. Profile: {Profile}. TargetUrl: {TargetUrl}",
            customerId,
            pushRequest.ExternalUserId,
            pushRequest.Type,
            pushRequest.ReferenceId,
            pushRequest.TitleEn,
            pushRequest.BodyEn,
            pushRequest.Profile,
            pushRequest.TargetUrl);
    }

    private void LogPushDispatchResult(
        Guid customerId,
        OneSignalMobilePushRequest pushRequest,
        OneSignalPushDispatchResult pushResult)
    {
        _logger.LogWarning(
            "[PUSH-DIAG] Admin customer OneSignal push result. CustomerId: {CustomerId}. ExternalId: {ExternalId}. Type: {NotificationType}. ReferenceId: {ReferenceId}. Attempted: {Attempted}. Sent: {Sent}. Skipped: {Skipped}. ProviderStatusCode: {ProviderStatusCode}. ProviderNotificationId: {ProviderNotificationId}. Reason: {Reason}",
            customerId,
            pushRequest.ExternalUserId,
            pushRequest.Type,
            pushRequest.ReferenceId,
            pushResult.Attempted,
            pushResult.Sent,
            pushResult.Skipped,
            pushResult.ProviderStatusCode,
            pushResult.ProviderNotificationId,
            pushResult.Reason);
    }
}
