using FluentValidation;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorContact;

public record AdminUpdateVendorContactCommand(
    Guid VendorId,
    string Region,
    string City,
    string NationalAddress) : IRequest<VendorDetailDto>;

public class AdminUpdateVendorContactCommandValidator : AbstractValidator<AdminUpdateVendorContactCommand>
{
    public AdminUpdateVendorContactCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.Region).NotEmpty().MaximumLength(100);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NationalAddress).NotEmpty().MaximumLength(500);
    }
}

public class AdminUpdateVendorContactCommandHandler : IRequestHandler<AdminUpdateVendorContactCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminUpdateVendorContactCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IVendorCommunicationService vendorCommunicationService,
        IUnitOfWork unitOfWork)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _vendorCommunicationService = vendorCommunicationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<VendorDetailDto> Handle(AdminUpdateVendorContactCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.UpdateContact(request.Region, request.City, request.NationalAddress);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_contact_updated",
                "تم تحديث عنوان المتجر",
                "Vendor contact details updated",
                "تم تحديث بيانات العنوان والتواصل من لوحة الإدارة.",
                "Your contact and address details were updated by the admin team.",
                "/profile",
                vendor.Id),
            cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
