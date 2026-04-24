using FluentValidation;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorOperationsSettings;

public record AdminUpdateVendorOperationsSettingsCommand(
    Guid VendorId,
    bool AcceptOrders,
    decimal? MinimumOrderAmount,
    int? PreparationTimeMinutes) : IRequest<VendorDetailDto>;

public class AdminUpdateVendorOperationsSettingsCommandValidator : AbstractValidator<AdminUpdateVendorOperationsSettingsCommand>
{
    public AdminUpdateVendorOperationsSettingsCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.MinimumOrderAmount).GreaterThanOrEqualTo(0).When(x => x.MinimumOrderAmount.HasValue);
        RuleFor(x => x.PreparationTimeMinutes).GreaterThanOrEqualTo(0).When(x => x.PreparationTimeMinutes.HasValue);
    }
}

public class AdminUpdateVendorOperationsSettingsCommandHandler : IRequestHandler<AdminUpdateVendorOperationsSettingsCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AdminUpdateVendorOperationsSettingsCommandHandler(
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

    public async Task<VendorDetailDto> Handle(AdminUpdateVendorOperationsSettingsCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.UpdateOperationsSettings(request.AcceptOrders, request.MinimumOrderAmount, request.PreparationTimeMinutes);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorReviewAuditService.AppendActivityEntryAsync(
            vendor.UserId,
            "operations-settings-updated",
            "info",
            $"Operations settings updated. Accept orders: {(request.AcceptOrders ? "enabled" : "disabled")}, minimum order: {request.MinimumOrderAmount?.ToString("0.##") ?? "not set"}, preparation time: {request.PreparationTimeMinutes?.ToString() ?? "not set"} minutes.",
            "Operations Console",
            "Admin",
            _currentUserService.UserId,
            cancellationToken: cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_operations_settings_updated",
                "تم تحديث إعدادات تشغيل المتجر",
                "Vendor operations settings updated",
                "تم تحديث إعدادات قبول الطلبات والحد الأدنى وزمن التحضير من لوحة الإدارة.",
                "Your order acceptance, minimum order, and preparation settings were updated by the admin team.",
                "/profile",
                vendor.Id),
            cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
