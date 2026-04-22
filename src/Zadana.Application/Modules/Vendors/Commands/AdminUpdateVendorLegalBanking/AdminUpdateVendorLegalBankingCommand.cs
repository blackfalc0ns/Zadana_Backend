using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorLegalBanking;

public record AdminUpdateVendorLegalBankingCommand(
    Guid VendorId,
    string CommercialRegistrationNumber,
    DateTime? CommercialRegistrationExpiryDate,
    string? TaxId,
    string? LicenseNumber,
    string BankName,
    string AccountHolderName,
    string Iban,
    string? SwiftCode,
    string? PayoutCycle,
    string? CommercialRegisterDocumentUrl,
    string? TaxDocumentUrl,
    string? LicenseDocumentUrl) : IRequest<VendorDetailDto>;

public class AdminUpdateVendorLegalBankingCommandValidator : AbstractValidator<AdminUpdateVendorLegalBankingCommand>
{
    public AdminUpdateVendorLegalBankingCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.CommercialRegistrationNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TaxId).MaximumLength(50);
        RuleFor(x => x.LicenseNumber).MaximumLength(100);
        RuleFor(x => x.BankName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountHolderName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Iban).NotEmpty().MaximumLength(34);
        RuleFor(x => x.SwiftCode).MaximumLength(11);
        RuleFor(x => x.PayoutCycle).MaximumLength(50);
    }
}

public class AdminUpdateVendorLegalBankingCommandHandler : IRequestHandler<AdminUpdateVendorLegalBankingCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminUpdateVendorLegalBankingCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IUnitOfWork unitOfWork)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _unitOfWork = unitOfWork;
    }

    public async Task<VendorDetailDto> Handle(AdminUpdateVendorLegalBankingCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.UpdateLegal(
            request.CommercialRegistrationNumber,
            request.CommercialRegistrationExpiryDate,
            request.TaxId,
            request.LicenseNumber,
            request.CommercialRegisterDocumentUrl,
            request.TaxDocumentUrl,
            request.LicenseDocumentUrl);
        vendor.UpdateBanking(request.PayoutCycle);

        foreach (var account in vendor.BankAccounts)
        {
            account.UnsetPrimary();
        }

        var primaryAccount = vendor.BankAccounts
            .OrderByDescending(account => account.IsPrimary)
            .ThenBy(account => account.CreatedAtUtc)
            .FirstOrDefault();

        if (primaryAccount == null)
        {
            primaryAccount = new VendorBankAccount(
                vendor.Id,
                request.BankName,
                request.AccountHolderName,
                request.Iban,
                request.SwiftCode);

            primaryAccount.MarkAsPreferredForSetup();
            _vendorRepository.AddBankAccount(primaryAccount);
        }
        else
        {
            primaryAccount.UpdateDetails(request.BankName, request.AccountHolderName, request.Iban, request.SwiftCode);
            primaryAccount.MarkAsPreferredForSetup();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
