using MediatR;
using Zadana.Application.Common.Interfaces;
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

    public RegisterVendorCommandHandler(
        IRegistrationWorkflow registrationWorkflow,
        IVendorRepository vendorRepository,
        IUnitOfWork unitOfWork)
    {
        _registrationWorkflow = registrationWorkflow;
        _vendorRepository = vendorRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResponseDto> Handle(RegisterVendorCommand request, CancellationToken cancellationToken)
    {
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
                request.BranchAddressLine,
                request.BranchLatitude,
                request.BranchLongitude,
                request.BranchContactPhone,
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

            var authResponse = await _registrationWorkflow.BuildAuthResponseAsync(
                user,
                cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return authResponse;
        }
        catch
        {
            await _registrationWorkflow.CompensateAccountCreationFailureAsync(user.Id, cancellationToken);
            throw;
        }
    }
}
