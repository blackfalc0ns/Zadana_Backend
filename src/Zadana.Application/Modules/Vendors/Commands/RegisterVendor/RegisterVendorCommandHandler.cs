using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Geography.Support;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Application.Modules.Vendors.Commands.RegisterVendor;

public class RegisterVendorCommandHandler : IRequestHandler<RegisterVendorCommand, AuthResponseDto>
{
    private readonly IRegistrationWorkflow _registrationWorkflow;
    private readonly IVendorRepository _vendorRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAdminAlertService _adminAlertService;
    private readonly IApplicationDbContext _context;

    public RegisterVendorCommandHandler(
        IRegistrationWorkflow registrationWorkflow,
        IVendorRepository vendorRepository,
        IUnitOfWork unitOfWork,
        IAdminAlertService adminAlertService,
        IApplicationDbContext context)
    {
        _registrationWorkflow = registrationWorkflow;
        _vendorRepository = vendorRepository;
        _unitOfWork = unitOfWork;
        _adminAlertService = adminAlertService;
        _context = context;
    }

    public async Task<AuthResponseDto> Handle(RegisterVendorCommand request, CancellationToken cancellationToken)
    {
        await OperationalGeographyScope.EnsureOperationalRegionCityAsync(
            _context,
            request.Region,
            request.City,
            cancellationToken);

        var user = await _registrationWorkflow.RegisterAccountAsync(
            new CreateIdentityAccountRequest(
                request.FullName,
                request.Email,
                request.Phone,
                UserRole.Vendor,
                request.Password),
            cancellationToken);
        try
        {
            var vendor = new Vendor(
                user.Id,
                request.BusinessNameAr,
                request.BusinessNameEn,
                request.BusinessType,
                request.CommercialRegistrationNumber,
                request.ContactEmail,
                request.ContactPhone,
                request.TaxId,
                request.DescriptionAr,
                request.DescriptionEn,
                request.OwnerName,
                request.OwnerEmail,
                request.OwnerPhone,
                request.IdNumber,
                request.Nationality,
                request.Region,
                request.City,
                request.NationalAddress,
                request.CommercialRegistrationExpiryDate,
                request.LicenseNumber,
                request.PayoutCycle,
                request.LogoUrl,
                request.CommercialRegisterDocumentUrl,
                request.TaxDocumentUrl,
                request.LicenseDocumentUrl);

            _vendorRepository.Add(vendor);
            var branch = new VendorBranch(
                vendor.Id,
                request.BranchName,
                request.BranchName,
                false,
                request.BranchAddressLine,
                request.Region,
                request.City,
                request.BranchLatitude,
                request.BranchLongitude,
                request.BranchContactPhone,
                string.Empty,
                string.Empty,
                request.BranchDeliveryRadiusKm);

            _vendorRepository.AddBranch(branch);
            branch.OperatingHours.Add(new BranchOperatingHour(branch.Id, 0, new TimeSpan(9, 0, 0), new TimeSpan(22, 0, 0)));
            branch.OperatingHours.Add(new BranchOperatingHour(branch.Id, 1, new TimeSpan(9, 0, 0), new TimeSpan(22, 0, 0)));
            branch.OperatingHours.Add(new BranchOperatingHour(branch.Id, 2, new TimeSpan(9, 0, 0), new TimeSpan(22, 0, 0)));
            branch.OperatingHours.Add(new BranchOperatingHour(branch.Id, 3, new TimeSpan(9, 0, 0), new TimeSpan(22, 0, 0)));
            branch.OperatingHours.Add(new BranchOperatingHour(branch.Id, 4, new TimeSpan(9, 0, 0), new TimeSpan(22, 0, 0)));
            branch.OperatingHours.Add(new BranchOperatingHour(branch.Id, 5, new TimeSpan(9, 0, 0), new TimeSpan(23, 0, 0)));
            branch.OperatingHours.Add(new BranchOperatingHour(branch.Id, 6, new TimeSpan(14, 0, 0), new TimeSpan(23, 30, 0)));

            var bankAccount = new VendorBankAccount(
                vendor.Id,
                request.BankName,
                request.AccountHolderName,
                request.Iban,
                request.SwiftCode);
            bankAccount.MarkAsPreferredForSetup();
            _vendorRepository.AddBankAccount(bankAccount);

            user = await _registrationWorkflow.SendRegistrationOtpAsync(user, cancellationToken);

            var authResponse = await _registrationWorkflow.BuildAuthResponseAsync(
                user,
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _adminAlertService.SendAsync(
                new AdminAlertRequest(
                    AdminAlertTypes.VendorApprovalRequested,
                    AdminAlertCategories.Vendors,
                    AdminAlertPriorities.High,
                    "تاجر جديد يحتاج مراجعة",
                    "New vendor requires review",
                    $"قام التاجر {vendor.BusinessNameAr} بإرسال طلب الانضمام وبانتظار مراجعة الإدارة.",
                    $"Vendor {vendor.BusinessNameEn} submitted an onboarding request.",
                    vendor.Id,
                    $"/vendors/{vendor.Id}",
                    new
                    {
                        vendorId = vendor.Id,
                        vendorUserId = vendor.UserId,
                        businessNameAr = vendor.BusinessNameAr,
                        businessNameEn = vendor.BusinessNameEn,
                        city = vendor.City,
                        region = vendor.Region
                    }),
                cancellationToken);

            return authResponse;
        }
        catch
        {
            await _registrationWorkflow.CompensateAccountCreationFailureAsync(user.Id, cancellationToken);
            throw;
        }
    }
}
