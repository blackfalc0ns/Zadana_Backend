using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorStore;

public record AdminUpdateVendorStoreCommand(
    Guid VendorId,
    string BusinessNameAr,
    string BusinessNameEn,
    string BusinessType,
    string ContactEmail,
    string ContactPhone,
    string? DescriptionAr,
    string? DescriptionEn,
    string? LogoUrl,
    string? CommercialRegisterDocumentUrl,
    string? Region,
    string? City,
    string? NationalAddress,
    string? CommercialRegistrationNumber) : IRequest<VendorDetailDto>;

public class AdminUpdateVendorStoreCommandValidator : AbstractValidator<AdminUpdateVendorStoreCommand>
{
    public AdminUpdateVendorStoreCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.BusinessNameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.ContactPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Region).MaximumLength(100);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.NationalAddress).MaximumLength(500);
        RuleFor(x => x.CommercialRegistrationNumber).MaximumLength(50);
    }
}

public class AdminUpdateVendorStoreCommandHandler : IRequestHandler<AdminUpdateVendorStoreCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminUpdateVendorStoreCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IUnitOfWork unitOfWork)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _unitOfWork = unitOfWork;
    }

    public async Task<VendorDetailDto> Handle(AdminUpdateVendorStoreCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.UpdateStore(
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
            request.CommercialRegistrationNumber);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
