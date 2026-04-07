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
    private readonly IUnitOfWork _unitOfWork;

    public AdminUpdateVendorContactCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IUnitOfWork unitOfWork)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _unitOfWork = unitOfWork;
    }

    public async Task<VendorDetailDto> Handle(AdminUpdateVendorContactCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.UpdateContact(request.Region, request.City, request.NationalAddress);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
