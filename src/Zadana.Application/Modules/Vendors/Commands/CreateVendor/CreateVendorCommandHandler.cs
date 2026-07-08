using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.CreateVendor;

public class CreateVendorCommandHandler : IRequestHandler<CreateVendorCommand, Guid>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IAdminAlertService _adminAlertService;

    public CreateVendorCommandHandler(
        IVendorRepository vendorRepository,
        IUnitOfWork unitOfWork,
        IIdentityAccountService identityAccountService,
        IAdminAlertService adminAlertService)
    {
        _vendorRepository = vendorRepository;
        _unitOfWork = unitOfWork;
        _identityAccountService = identityAccountService;
        _adminAlertService = adminAlertService;
    }

    public async Task<Guid> Handle(CreateVendorCommand request, CancellationToken cancellationToken)
    {
        // 1. Check if user exists
        var userExists = await _identityAccountService.ExistsByIdAsync(request.OwnerUserId, cancellationToken);
        if (!userExists)
        {
            throw new NotFoundException("User", request.OwnerUserId);
        }

        // 2. Map properties to the Domain Entity
        // Note: The Domain Entity expects BusinessNameAr and BusinessNameEn, while the command
        // provides LegalName and DisplayName. For demo purposes we map them respectively.
        var vendor = new Vendor(
            userId: request.OwnerUserId,
            businessNameAr: request.LegalName,
            businessNameEn: request.DisplayName,
            businessType: "General Retail", // Using a default as command doesn't have it
            commercialRegistrationNumber: request.CommercialRegister ?? string.Empty,
            contactEmail: request.SupportEmail ?? string.Empty,
            contactPhone: request.SupportPhone ?? string.Empty,
            taxId: request.TaxNumber);

        // 3. Save to database
        _vendorRepository.Add(vendor);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.VendorApprovalRequested,
                AdminAlertCategories.Vendors,
                AdminAlertPriorities.High,
                "تاجر جديد تحت المراجعة",
                "New vendor pending review",
                $"أنشأنا التاجر {vendor.BusinessNameAr} وهو الآن بانتظار مراجعة الإدارة.",
                $"Vendor {vendor.BusinessNameEn} was created and is now pending admin review.",
                vendor.Id,
                $"/vendors/{vendor.Id}",
                new
                {
                    vendorId = vendor.Id,
                    vendorUserId = vendor.UserId,
                    businessNameAr = vendor.BusinessNameAr,
                    businessNameEn = vendor.BusinessNameEn,
                    status = vendor.Status.ToString()
                }),
            cancellationToken);

        // 4. Return new ID
        return vendor.Id;
    }
}
