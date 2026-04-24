using FluentValidation;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorNotificationSettings;

public record AdminUpdateVendorNotificationSettingsCommand(
    Guid VendorId,
    bool EmailNotificationsEnabled,
    bool SmsNotificationsEnabled,
    bool NewOrdersNotificationsEnabled) : IRequest<VendorDetailDto>;

public class AdminUpdateVendorNotificationSettingsCommandValidator : AbstractValidator<AdminUpdateVendorNotificationSettingsCommand>
{
    public AdminUpdateVendorNotificationSettingsCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
    }
}

public class AdminUpdateVendorNotificationSettingsCommandHandler : IRequestHandler<AdminUpdateVendorNotificationSettingsCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AdminUpdateVendorNotificationSettingsCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IVendorReviewAuditService vendorReviewAuditService,
        IVendorCommunicationService vendorCommunicationService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _vendorReviewAuditService = vendorReviewAuditService;
        _vendorCommunicationService = vendorCommunicationService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<VendorDetailDto> Handle(AdminUpdateVendorNotificationSettingsCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.UpdateNotificationSettings(
            request.EmailNotificationsEnabled,
            request.SmsNotificationsEnabled,
            request.NewOrdersNotificationsEnabled);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorReviewAuditService.AppendActivityEntryAsync(
            vendor.UserId,
            "notification-settings-updated",
            "info",
            $"Notification settings updated. Email: {(request.EmailNotificationsEnabled ? "enabled" : "disabled")}, SMS: {(request.SmsNotificationsEnabled ? "enabled" : "disabled")}, new orders: {(request.NewOrdersNotificationsEnabled ? "enabled" : "disabled")}.",
            "Operations Console",
            "Admin",
            _currentUserService.UserId,
            cancellationToken: cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_notification_settings_updated",
                "تم تحديث إعدادات الإشعارات",
                "Vendor notification settings updated",
                "تم تحديث تفضيلات إشعارات حسابك من لوحة الإدارة.",
                "Your vendor notification preferences were updated by the admin team.",
                "/profile",
                vendor.Id,
                SendEmail: true),
            cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
