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
    private readonly IUnitOfWork _unitOfWork;

    public AdminUpdateVendorNotificationSettingsCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IUnitOfWork unitOfWork)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _unitOfWork = unitOfWork;
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

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
