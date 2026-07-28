using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Text.Json;
using Zadana.Api.Common.Export;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Vendors.Requests;
using Zadana.Application.Common.Export;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Queries.GetVendorProducts;
using Zadana.Application.Modules.Orders.Queries.GetVendorOrders;
using Zadana.Application.Modules.Orders.Queries.GetVendorOrderStats;
using Zadana.Application.Modules.Vendors.Queries.GetAllVendors;
using Zadana.Application.Modules.Vendors.Commands.AdminResetVendorPassword;
using Zadana.Application.Modules.Vendors.Commands.AddVendorReviewNote;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorLegalBanking;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorFinanceSettings;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorContact;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorHours;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorNotificationSettings;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorOwner;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorOperationsSettings;
using Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorStore;
using Zadana.Application.Modules.Vendors.Commands.ApproveVendorDocumentReview;
using Zadana.Application.Modules.Vendors.Commands.ApproveVendor;
using Zadana.Application.Modules.Vendors.Commands.ArchiveVendor;
using Zadana.Application.Modules.Vendors.Commands.LockVendorLogin;
using Zadana.Application.Modules.Vendors.Commands.RejectVendorDocumentReview;
using Zadana.Application.Modules.Vendors.Commands.RejectVendor;
using Zadana.Application.Modules.Vendors.Commands.ReactivateVendor;
using Zadana.Application.Modules.Vendors.Commands.ReopenVendorReview;
using Zadana.Application.Modules.Vendors.Commands.ReviewVendorProfileFields;
using Zadana.Application.Modules.Vendors.Commands.RequestVendorDocuments;
using Zadana.Application.Modules.Vendors.Commands.StartVendorReview;
using Zadana.Application.Modules.Vendors.Commands.SuspendVendor;
using Zadana.Application.Modules.Vendors.Commands.UnlockVendorLogin;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Queries.GetAdminVendorStats;
using Zadana.Application.Modules.Vendors.Queries.GetVendorAnalytics;
using Zadana.Application.Modules.Vendors.Queries.GetVendorActivityLog;
using Zadana.Application.Modules.Vendors.Queries.GetAdminVendorFinanceSummary;
using Zadana.Application.Modules.Vendors.Queries.GetVendorDetail;
using Zadana.Application.Modules.Wallets.Commands.CreateSettlement;
using Zadana.Application.Modules.Wallets.Commands.CompleteVendorPayout;
using Zadana.Application.Modules.Wallets.Commands.EscalateVendorPayout;
using Zadana.Application.Modules.Wallets.Commands.RetryVendorPayout;
using Zadana.Application.Modules.Wallets.Commands.SuspendVendorPayout;
using Zadana.Application.Modules.Wallets.Queries.GetVendorPayouts;
using Zadana.Application.Modules.Wallets.Queries.GetVendorSettlements;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/admin/vendors")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin Dashboard API")]
public class AdminVendorsController : ApiControllerBase
{
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly IVendorCommunicationService _vendorCommunicationService;

    public AdminVendorsController(
        IStringLocalizer<SharedResource> localizer,
        IApplicationDbContext context,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        IVendorCommunicationService vendorCommunicationService)
    {
        _localizer = localizer;
        _context = context;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _vendorCommunicationService = vendorCommunicationService;
    }

    /// <summary>
    /// عرض قائمة التجار مع التصفية والبحث والترقيم
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllVendors(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] string? city,
        [FromQuery] string? region,
        [FromQuery] bool? isLoginLocked,
        [FromQuery] string? riskLevel,
        [FromQuery] string? verificationStatus,
        [FromQuery] string? documentsStatus,
        [FromQuery] string? payoutStatus,
        [FromQuery] string? onboardingStage,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await Sender.Send(new GetAllVendorsQuery(
            ResolveVendorStatusFilter(status),
            search,
            city,
            region,
            isLoginLocked,
            riskLevel,
            verificationStatus,
            documentsStatus,
            payoutStatus,
            onboardingStage,
            page,
            pageSize));
        return Ok(result);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetVendorStats(CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetAdminVendorStatsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportVendors(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] Guid[]? ids,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetAllVendorsQuery(
            ResolveVendorStatusFilter(status),
            search,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            1,
            ExportLimits.MaxRows), cancellationToken);
        var items = result.Items.AsEnumerable();
        if (ids is { Length: > 0 })
        {
            var selected = ids.ToHashSet();
            items = items.Where(item => selected.Contains(item.Id));
        }

        var file = ExcelExportBuilder.BuildFromObjects(
            ExportFileResult.StampFileName("vendors", ".xlsx"),
            ExportText.Label("Vendors", "التجار"),
            [
                ExportText.Column("ID", "المعرّف", "id"),
                ExportText.Column("Business Name AR", "الاسم التجاري عربي", "nameAr"),
                ExportText.Column("Business Name EN", "الاسم التجاري إنجليزي", "nameEn"),
                ExportText.Column("Owner", "المالك", "owner"),
                ExportText.Column("Email", "البريد", "email"),
                ExportText.Column("Phone", "الهاتف", "phone"),
                ExportText.Column("Status", "الحالة", "status"),
                ExportText.Column("City", "المدينة", "city"),
                ExportText.Column("Created At", "تاريخ الإنشاء", "createdAt")
            ],
            items,
            vendor => new Dictionary<string, string?>
            {
                ["id"] = vendor.Id.ToString(),
                ["nameAr"] = vendor.BusinessNameAr,
                ["nameEn"] = vendor.BusinessNameEn,
                ["owner"] = vendor.OwnerName,
                ["email"] = vendor.ContactEmail,
                ["phone"] = vendor.ContactPhone,
                ["status"] = vendor.Status,
                ["city"] = vendor.City,
                ["createdAt"] = vendor.CreatedAtUtc.ToString("o")
            });

        return ExportFileResult.From(file);
    }

    /// <summary>
    /// عرض تفاصيل تاجر معين
    /// </summary>
    [HttpGet("{vendorId:guid}")]
    public async Task<IActionResult> GetVendorDetail(Guid vendorId)
    {
        var result = await Sender.Send(new GetVendorDetailQuery(vendorId));
        return Ok(result);
    }

    [HttpGet("{vendorId:guid}/activity-log")]
    public async Task<IActionResult> GetVendorActivityLog(
        Guid vendorId,
        [FromQuery] string? type = null,
        [FromQuery] string? severity = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Sender.Send(new GetVendorActivityLogQuery(
            vendorId,
            type,
            severity,
            dateFrom,
            dateTo,
            page,
            pageSize));

        return Ok(result);
    }

    [HttpGet("{vendorId:guid}/activity-log/export")]
    public async Task<IActionResult> ExportVendorActivityLog(
        Guid vendorId,
        [FromQuery] string? type = null,
        [FromQuery] string? severity = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetVendorActivityLogQuery(
            vendorId,
            type,
            severity,
            dateFrom,
            dateTo,
            1,
            ExportLimits.MaxRows), cancellationToken);

        var file = ExcelExportBuilder.BuildFromObjects(
            ExportFileResult.StampFileName($"vendor-activity-{vendorId:N}", ".xlsx"),
            ExportText.Label("Activity", "النشاط"),
            [
                ExportText.Column("Type", "النوع", "type"),
                ExportText.Column("Severity", "الخطورة", "severity"),
                ExportText.Column("Actor", "الفاعل", "actor"),
                ExportText.Column("Role", "الدور", "role"),
                ExportText.Column("Created At", "تاريخ الإنشاء", "createdAt"),
                ExportText.Column("Message", "الرسالة", "message")
            ],
            result.Items,
            entry => new Dictionary<string, string?>
            {
                ["type"] = entry.Type,
                ["severity"] = entry.Severity,
                ["actor"] = entry.ActorName,
                ["role"] = entry.RoleLabel,
                ["createdAt"] = entry.CreatedAtUtc.ToString("o"),
                ["message"] = entry.Message
            });

        return ExportFileResult.From(file);
    }

    /// <summary>
    /// Send notification to vendor (test or production)
    /// </summary>
    [HttpPost("{vendorId:guid}/notifications/send")]
    public async Task<ActionResult<AdminVendorNotificationResponse>> SendVendorNotification(
        Guid vendorId,
        [FromBody] AdminSendVendorNotificationRequest? request,
        CancellationToken cancellationToken = default)
    {
        return await SendVendorMessageInternal(vendorId, request ?? new AdminSendVendorNotificationRequest(), cancellationToken);
    }

    private async Task<ActionResult<AdminVendorNotificationResponse>> SendVendorMessageInternal(
        Guid vendorId,
        AdminSendVendorNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var vendor = await _context.Vendors
            .Where(item => item.Id == vendorId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Vendor", vendorId);

        var titleAr = string.IsNullOrWhiteSpace(request.TitleAr) ? "إشعار تجريبي للتاجر" : request.TitleAr.Trim();
        var titleEn = string.IsNullOrWhiteSpace(request.TitleEn) ? "Vendor test notification" : request.TitleEn.Trim();
        var bodyAr = string.IsNullOrWhiteSpace(request.BodyAr)
            ? "هذا إشعار تجريبي من واجهة المشرف للتأكد من وصول الإشعارات إلى التاجر."
            : request.BodyAr.Trim();
        var bodyEn = string.IsNullOrWhiteSpace(request.BodyEn)
            ? "This is a test notification sent from the admin API to verify vendor delivery."
            : request.BodyEn.Trim();
        var type = string.IsNullOrWhiteSpace(request.Type) ? "vendor_test" : request.Type.Trim();

        var dispatch = await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                type,
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                string.IsNullOrWhiteSpace(request.TargetUrl) ? "/alerts" : request.TargetUrl.Trim(),
                request.ReferenceId,
                request.SendInbox,
                request.SendPush,
                request.SendEmail,
                request.Data),
            cancellationToken);

        return Ok(new AdminVendorNotificationResponse(
            Message: "Vendor notification queued successfully.",
            VendorId: vendor.Id,
            UserId: vendor.UserId,
            ExternalId: vendor.UserId.ToString(),
            Type: type,
            InboxRequested: dispatch.InboxRequested,
            PushAttempted: dispatch.PushAttempted,
            PushSent: dispatch.PushSent,
            PushSkipped: dispatch.PushSkipped,
            PushStatusCode: dispatch.PushStatusCode,
            ProviderNotificationId: dispatch.ProviderNotificationId,
            PushReason: dispatch.PushReason,
            EmailAttempted: dispatch.EmailAttempted,
            EmailSent: dispatch.EmailSent,
            EmailSkipped: dispatch.EmailSkipped,
            EmailReason: dispatch.EmailReason));
    }

    [HttpGet("{vendorId:guid}/orders")]
    public async Task<IActionResult> GetVendorOrders(
        Guid vendorId,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? paymentStatus = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await Sender.Send(new GetVendorOrdersQuery(vendorId, search, status, paymentStatus, page, pageSize));
        return Ok(result);
    }

    [HttpGet("{vendorId:guid}/orders/stats")]
    public async Task<IActionResult> GetVendorOrderStats(Guid vendorId, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetVendorOrderStatsQuery(vendorId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{vendorId:guid}/orders/export")]
    public async Task<IActionResult> ExportVendorOrders(
        Guid vendorId,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? paymentStatus = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new GetVendorOrdersQuery(vendorId, search, status, paymentStatus, 1, ExportLimits.MaxRows),
            cancellationToken);

        var file = ExcelExportBuilder.BuildFromObjects(
            ExportFileResult.StampFileName($"vendor-orders-{vendorId:N}", ".xlsx"),
            ExportText.Label("Orders", "الطلبات"),
            [
                ExportText.Column("Order Number", "رقم الطلب", "orderNumber"),
                ExportText.Column("Customer", "العميل", "customer"),
                ExportText.Column("Status", "الحالة", "status"),
                ExportText.Column("Payment Status", "حالة الدفع", "paymentStatus"),
                ExportText.Column("Total", "الإجمالي", "total"),
                ExportText.Column("Items", "العناصر", "items"),
                ExportText.Column("Placed At", "تاريخ الطلب", "placedAt")
            ],
            result.Items,
            order => new Dictionary<string, string?>
            {
                ["orderNumber"] = order.OrderNumber,
                ["customer"] = order.CustomerName,
                ["status"] = order.Status,
                ["paymentStatus"] = order.PaymentStatus,
                ["total"] = order.TotalAmount.ToString("0.##"),
                ["items"] = order.ItemsCount.ToString(),
                ["placedAt"] = order.PlacedAtUtc.ToString("o")
            });

        return ExportFileResult.From(file);
    }

    [HttpGet("{vendorId:guid}/products")]
    public async Task<IActionResult> GetVendorProducts(
        Guid vendorId,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? branchId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await Sender.Send(new GetVendorProductsQuery(vendorId, categoryId, branchId, search, status, page, pageSize));
        return Ok(result);
    }

    [HttpGet("{vendorId:guid}/analytics")]
    public async Task<IActionResult> GetVendorAnalytics(
        Guid vendorId,
        [FromQuery] string range = "30d")
    {
        var result = await Sender.Send(new GetVendorAnalyticsQuery(vendorId, range));
        return Ok(result);
    }

    [HttpGet("{vendorId:guid}/finance-summary")]
    public async Task<IActionResult> GetVendorFinanceSummary(Guid vendorId)
    {
        var result = await Sender.Send(new GetAdminVendorFinanceSummaryQuery(vendorId));
        return Ok(result);
    }

    [HttpGet("{vendorId:guid}/settlements")]
    public async Task<IActionResult> GetVendorSettlements(
        Guid vendorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Sender.Send(new GetVendorSettlementsQuery(vendorId, page, pageSize));
        return Ok(result);
    }

    [HttpGet("{vendorId:guid}/payouts")]
    public async Task<IActionResult> GetVendorPayouts(
        Guid vendorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Sender.Send(new GetVendorPayoutsQuery(vendorId, page, pageSize));
        return Ok(result);
    }

    [HttpGet("{vendorId:guid}/payouts/{paymentId:guid}/receipt")]
    public async Task<IActionResult> ExportVendorPayoutReceipt(
        Guid vendorId,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetVendorPayoutsQuery(vendorId, 1, ExportLimits.MaxRows), cancellationToken);
        var payout = result.Items.FirstOrDefault(item => item.Id == paymentId);
        if (payout is null)
        {
            return NotFound();
        }

        var file = PdfExportBuilder.BuildReceipt(
            ExportFileResult.StampFileName($"payout-receipt-{payout.PayoutNumber}", ".pdf"),
            ExportText.Label("Payout Receipt", "إيصال دفعة"),
            [
                ExportText.Field("Vendor ID", "معرّف التاجر", vendorId.ToString()),
                ExportText.Field("Payment #", "رقم الدفعة", payout.PayoutNumber),
                ExportText.Field("Amount", "المبلغ", payout.Amount.ToString("0.##")),
                ExportText.Field("Status", "الحالة", payout.Status),
                ExportText.Field("Bank", "البنك", payout.BankName ?? string.Empty),
                ExportText.Field("IBAN", "الآيبان", payout.Iban ?? string.Empty),
                ExportText.Field("Reference", "المرجع", payout.TransferReference ?? string.Empty),
                ExportText.Field("Created At", "تاريخ الإنشاء", payout.CreatedAtUtc.ToString("o")),
                ExportText.Field("Processed At", "تاريخ المعالجة", payout.ProcessedAtUtc?.ToString("o") ?? string.Empty)
            ]);

        return ExportFileResult.From(file);
    }

    [HttpPost("{vendorId:guid}/settlements")]
    public async Task<IActionResult> CreateVendorSettlement(Guid vendorId, [FromBody] AdminCreateVendorSettlementRequest request)
    {
        var settlementId = await Sender.Send(new CreateSettlementCommand(
            vendorId,
            null,
            request.GrossAmount,
            request.CommissionAmount,
            request.NetAmount,
            request.RefundAmount,
            request.AdjustmentAmount,
            request.PeriodFrom,
            request.PeriodTo));

        return Ok(new { SettlementId = settlementId });
    }

    [HttpPost("{vendorId:guid}/start-review")]
    public async Task<IActionResult> StartVendorReview(Guid vendorId)
    {
        var result = await Sender.Send(new StartVendorReviewCommand(vendorId));
        return Ok(result);
    }

    [HttpPost("{vendorId:guid}/request-documents")]
    public async Task<IActionResult> RequestVendorDocuments(Guid vendorId, [FromBody] AdminRequestVendorDocumentsRequest request)
    {
        var result = await Sender.Send(new RequestVendorDocumentsCommand(vendorId, request.Note));
        return Ok(result);
    }

    [HttpPost("{vendorId:guid}/reopen-review")]
    public async Task<IActionResult> ReopenVendorReview(Guid vendorId)
    {
        var result = await Sender.Send(new ReopenVendorReviewCommand(vendorId));
        return Ok(result);
    }

    [HttpPost("{vendorId:guid}/review-notes")]
    public async Task<IActionResult> AddVendorReviewNote(Guid vendorId, [FromBody] AdminAddVendorReviewNoteRequest request)
    {
        var result = await Sender.Send(new AddVendorReviewNoteCommand(
            vendorId,
            request.Message,
            request.AuthorName,
            request.RoleLabel));

        return Ok(result);
    }

    [HttpPost("{vendorId:guid}/documents/{documentId}/approve")]
    public async Task<IActionResult> ApproveVendorDocument(Guid vendorId, string documentId)
    {
        var result = await Sender.Send(new ApproveVendorDocumentReviewCommand(vendorId, documentId));
        return Ok(result);
    }

    [HttpPost("{vendorId:guid}/documents/{documentId}/reject")]
    public async Task<IActionResult> RejectVendorDocument(Guid vendorId, string documentId, [FromBody] AdminRejectVendorDocumentRequest request)
    {
        var result = await Sender.Send(new RejectVendorDocumentReviewCommand(vendorId, documentId, request.Reason));
        return Ok(result);
    }

    [HttpPost("{vendorId:guid}/profile-fields/review")]
    public async Task<IActionResult> ReviewVendorProfileFields(Guid vendorId, [FromBody] AdminReviewVendorProfileFieldsRequest request)
    {
        var result = await Sender.Send(new ReviewVendorProfileFieldsCommand(
            vendorId,
            request.Items.Select(item => new ReviewVendorProfileFieldItem(item.Code, item.Decision, item.Reason)).ToList()));

        return Ok(result);
    }

    [HttpPost("{vendorId:guid}/payouts/{payoutId:guid}/retry")]
    public async Task<IActionResult> RetryVendorPayout(Guid vendorId, Guid payoutId)
    {
        await Sender.Send(new RetryVendorPayoutCommand(vendorId, payoutId));
        return Ok(new { Message = _localizer["VENDOR_PAYOUT_RETRY_SUCCESS"].Value });
    }

    [HttpPost("{vendorId:guid}/payouts/{payoutId:guid}/complete")]
    public async Task<IActionResult> CompleteVendorPayout(
        Guid vendorId,
        Guid payoutId,
        [FromBody] AdminCompleteVendorPayoutRequest? request)
    {
        await Sender.Send(new CompleteVendorPayoutCommand(
            vendorId,
            payoutId,
            request?.TransferReference,
            request?.ProofAttachmentId));
        return Ok(new { Message = _localizer["VENDOR_PAYOUT_COMPLETED_SUCCESS"].Value });
    }

    [HttpPost("{vendorId:guid}/payouts/{payoutId:guid}/suspend")]
    public async Task<IActionResult> SuspendVendorPayout(Guid vendorId, Guid payoutId)
    {
        await Sender.Send(new SuspendVendorPayoutCommand(vendorId, payoutId));
        return Ok(new { Message = _localizer["VENDOR_PAYOUT_SUSPENDED_SUCCESS"].Value });
    }

    [HttpPost("{vendorId:guid}/payouts/{payoutId:guid}/escalate")]
    public async Task<IActionResult> EscalateVendorPayout(Guid vendorId, Guid payoutId)
    {
        await Sender.Send(new EscalateVendorPayoutCommand(vendorId, payoutId));
        return Ok(new { Message = _localizer["VENDOR_PAYOUT_ESCALATED_SUCCESS"].Value });
    }

    /// <summary>
    /// اعتماد تاجر وتحديد نسبة العمولة
    /// </summary>
    [HttpPost("{vendorId:guid}/approve")]
    public async Task<IActionResult> ApproveVendor(Guid vendorId, [FromBody] ApproveVendorRequest request)
    {
        await Sender.Send(new ApproveVendorCommand(vendorId, request.CommissionRate));
        return Ok(new { Message = _localizer["VendorApprovedSuccessfully"].Value });
    }

    /// <summary>
    /// رفض تاجر مع ذكر السبب
    /// </summary>
    [HttpPost("{vendorId:guid}/reject")]
    public async Task<IActionResult> RejectVendor(Guid vendorId, [FromBody] RejectVendorRequest request)
    {
        await Sender.Send(new RejectVendorCommand(vendorId, request.Reason));
        return Ok(new { Message = _localizer["VendorRejected"].Value });
    }

    /// <summary>
    /// تعليق تاجر نشط
    /// </summary>
    [HttpPost("{vendorId:guid}/suspend")]
    public async Task<IActionResult> SuspendVendor(Guid vendorId, [FromBody] SuspendVendorRequest request)
    {
        await Sender.Send(new SuspendVendorCommand(vendorId, request.Reason));
        return Ok(new { Message = _localizer["VendorSuspended"].Value });
    }

    [HttpPost("{vendorId:guid}/reactivate")]
    public async Task<IActionResult> ReactivateVendor(Guid vendorId)
    {
        await Sender.Send(new ReactivateVendorCommand(vendorId));
        return Ok(new { Message = _localizer["VENDOR_REACTIVATED_SUCCESS"].Value });
    }

    [HttpPost("{vendorId:guid}/lock-login")]
    public async Task<IActionResult> LockLogin(Guid vendorId, [FromBody] LockVendorLoginRequest request)
    {
        await Sender.Send(new LockVendorLoginCommand(vendorId, request.Reason));
        return Ok(new { Message = _localizer["VENDOR_LOGIN_LOCKED_SUCCESS"].Value });
    }

    [HttpPost("{vendorId:guid}/unlock-login")]
    public async Task<IActionResult> UnlockLogin(Guid vendorId)
    {
        await Sender.Send(new UnlockVendorLoginCommand(vendorId));
        return Ok(new { Message = _localizer["VENDOR_LOGIN_UNLOCKED_SUCCESS"].Value });
    }

    [HttpPost("{vendorId:guid}/archive")]
    public async Task<IActionResult> ArchiveVendor(Guid vendorId, [FromBody] ArchiveVendorRequest request)
    {
        await Sender.Send(new ArchiveVendorCommand(vendorId, request.Reason));
        return Ok(new { Message = _localizer["VENDOR_ARCHIVED_SUCCESS"].Value });
    }

    [HttpPost("{vendorId:guid}/reset-password")]
    public async Task<IActionResult> ResetVendorPassword(Guid vendorId, [FromBody] AdminResetVendorPasswordRequest request)
    {
        await Sender.Send(new AdminResetVendorPasswordCommand(vendorId, request.NewPassword));
        return Ok(new { Message = _localizer["VENDOR_PASSWORD_RESET_SUCCESS"].Value });
    }

    [HttpPut("{vendorId:guid}/store")]
    public async Task<IActionResult> UpdateStore(Guid vendorId, [FromBody] AdminUpdateVendorStoreRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorStoreCommand(
            vendorId,
            request.BusinessNameAr,
            request.BusinessNameEn,
            request.BusinessType,
            request.ContactEmail,
            request.ContactPhone,
            request.DescriptionAr,
            request.DescriptionEn,
            request.LogoUrl,
            request.CommercialRegisterDocumentUrl,
            request.Region,
            request.City,
            request.NationalAddress,
            request.CommercialRegistrationNumber));

        return Ok(result);
    }

    [HttpPut("{vendorId:guid}/contact")]
    public async Task<IActionResult> UpdateContact(Guid vendorId, [FromBody] AdminUpdateVendorContactRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorContactCommand(
            vendorId,
            request.Region,
            request.City,
            request.NationalAddress,
            request.BranchLatitude,
            request.BranchLongitude));

        return Ok(result);
    }

    [HttpPut("{vendorId:guid}/owner")]
    public async Task<IActionResult> UpdateOwner(Guid vendorId, [FromBody] AdminUpdateVendorOwnerRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorOwnerCommand(
            vendorId,
            request.OwnerName,
            request.OwnerEmail,
            request.OwnerPhone,
            request.IdNumber,
            request.Nationality));

        return Ok(result);
    }

    [HttpPut("{vendorId:guid}/legal-banking")]
    public async Task<IActionResult> UpdateLegalBanking(Guid vendorId, [FromBody] AdminUpdateVendorLegalBankingRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorLegalBankingCommand(
            vendorId,
            request.CommercialRegistrationNumber,
            request.CommercialRegistrationExpiryDate,
            request.TaxId,
            request.LicenseNumber,
            request.BankName,
            request.AccountHolderName,
            request.Iban,
            request.SwiftCode,
            request.PayoutCycle,
            request.CommercialRegisterDocumentUrl,
            request.TaxDocumentUrl,
            request.LicenseDocumentUrl,
            request.PayoutDay));

        return Ok(result);
    }

    [HttpPut("{vendorId:guid}/finance-settings")]
    public async Task<IActionResult> UpdateFinanceSettings(Guid vendorId, [FromBody] AdminUpdateVendorFinanceSettingsRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorFinanceSettingsCommand(
            vendorId,
            request.FinancialLifecycleMode,
            request.PayoutCycle,
            request.PayoutDay));

        return Ok(result);
    }

    [HttpPut("{vendorId:guid}/hours")]
    public async Task<IActionResult> UpdateHours(Guid vendorId, [FromBody] AdminUpdateVendorHoursRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorHoursCommand(
            vendorId,
            request.Hours.Select(item => new AdminUpdateVendorHoursItem(
                item.DayOfWeek,
                item.OpenTime,
                item.CloseTime,
                item.IsOpen)).ToList()));

        return Ok(result);
    }

    [HttpPut("{vendorId:guid}/operations-settings")]
    public async Task<IActionResult> UpdateOperationsSettings(Guid vendorId, [FromBody] AdminUpdateVendorOperationsSettingsRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorOperationsSettingsCommand(
            vendorId,
            request.AcceptOrders,
            request.MinimumOrderAmount,
            request.PreparationTimeMinutes));

        return Ok(result);
    }

    [HttpPut("{vendorId:guid}/notification-settings")]
    public async Task<IActionResult> UpdateNotificationSettings(Guid vendorId, [FromBody] AdminUpdateVendorNotificationSettingsRequest request)
    {
        var result = await Sender.Send(new AdminUpdateVendorNotificationSettingsCommand(
            vendorId,
            request.EmailNotificationsEnabled,
            request.SmsNotificationsEnabled,
            request.NewOrdersNotificationsEnabled,
            request.NotificationSound));

        return Ok(result);
    }

    /// <summary>
    /// تعديل نسبة عمولة التاجر
    /// </summary>
    [HttpPut("{vendorId:guid}/commission-rate")]
    public async Task<IActionResult> UpdateCommissionRate(
        Guid vendorId,
        [FromBody] UpdateCommissionRateRequest request)
    {
        var vendor = await _context.Vendors.FindAsync(vendorId)
            ?? throw new NotFoundException("Vendor", vendorId);

        vendor.UpdateCommissionRate(request.CommissionRate);
        await _context.SaveChangesAsync(default);

        return Ok(new { Message = _localizer["COMMISSION_RATE_UPDATED"].Value, vendor.CommissionRate });
    }

    private static VendorStatus? ResolveVendorStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (string.Equals(status.Trim(), "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return VendorStatus.PendingReview;
        }

        return Enum.TryParse<VendorStatus>(status.Trim(), true, out var parsed)
            ? parsed
            : null;
    }
}

public record UpdateCommissionRateRequest(decimal CommissionRate);
