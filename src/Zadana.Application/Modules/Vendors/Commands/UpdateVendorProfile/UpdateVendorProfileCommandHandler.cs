using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorProfile;

public class UpdateVendorProfileCommandHandler : IRequestHandler<UpdateVendorProfileCommand, VendorProfileDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UpdateVendorProfileCommandHandler(
        IVendorRepository vendorRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _vendorRepository = vendorRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<VendorProfileDto> Handle(UpdateVendorProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        vendor.UpdateProfile(
            request.BusinessNameAr,
            request.BusinessNameEn,
            request.BusinessType,
            request.ContactEmail,
            request.ContactPhone,
            request.TaxId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new VendorProfileDto(
            vendor.Id,
            vendor.BusinessNameAr,
            vendor.BusinessNameEn,
            vendor.BusinessType,
            vendor.CommercialRegistrationNumber,
            vendor.TaxId,
            vendor.ContactEmail,
            vendor.ContactPhone,
            vendor.CommissionRate,
            vendor.Status.ToString(),
            vendor.LogoUrl,
            vendor.ApprovedAtUtc,
            vendor.CreatedAtUtc);
    }
}
