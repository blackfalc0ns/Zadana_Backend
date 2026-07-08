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
    private readonly IProfileChangeApprovalService _profileChangeApprovalService;

    public UpdateVendorProfileCommandHandler(
        IVendorRepository vendorRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IProfileChangeApprovalService profileChangeApprovalService)
    {
        _vendorRepository = vendorRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _profileChangeApprovalService = profileChangeApprovalService;
    }

    public async Task<VendorProfileDto> Handle(UpdateVendorProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        var requestedTaxId = string.IsNullOrWhiteSpace(request.TaxId) ? null : request.TaxId.Trim();
        var taxIdChanged = !string.Equals(
            vendor.TaxId?.Trim(),
            requestedTaxId,
            StringComparison.OrdinalIgnoreCase);

        vendor.UpdateProfile(
            request.BusinessNameAr,
            request.BusinessNameEn,
            request.BusinessType,
            request.ContactEmail,
            request.ContactPhone,
            taxIdChanged ? vendor.TaxId : requestedTaxId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (taxIdChanged)
        {
            await _profileChangeApprovalService.SubmitAsync(
                userId,
                vendor.UserId,
                ProfileChangeApprovalActions.VendorProfileBasic,
                $"Vendor {vendor.BusinessNameEn} requested tax id changes.",
                new VendorBasicProfileChangePayload(
                    vendor.Id,
                    request.BusinessNameAr,
                    request.BusinessNameEn,
                    request.BusinessType,
                    request.ContactEmail,
                    request.ContactPhone,
                    requestedTaxId),
                new ProfileChangeApprovalAlert(
                    AdminAlertTypes.VendorCriticalChangeSubmitted,
                    AdminAlertCategories.Vendors,
                    AdminAlertPriorities.High,
                    "تعديل رقم ضريبي بانتظار الاعتماد",
                    "Vendor tax id change pending approval",
                    $"أرسل التاجر {vendor.BusinessNameAr} تعديل الرقم الضريبي وينتظر اعتماد المشرف.",
                    $"Vendor {vendor.BusinessNameEn} submitted tax id changes pending admin approval.",
                    vendor.Id,
                    "/admin/access/approvals",
                    new { vendorId = vendor.Id, userId = vendor.UserId, section = "basic", sensitiveFields = new[] { "taxId" } }),
                cancellationToken);
        }

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
