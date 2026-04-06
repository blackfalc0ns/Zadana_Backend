using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorOwner;

public record AdminUpdateVendorOwnerCommand(
    Guid VendorId,
    string OwnerName,
    string OwnerEmail,
    string OwnerPhone,
    string? IdNumber,
    string? Nationality) : IRequest<VendorDetailDto>;

public class AdminUpdateVendorOwnerCommandValidator : AbstractValidator<AdminUpdateVendorOwnerCommand>
{
    public AdminUpdateVendorOwnerCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OwnerEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.OwnerPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.IdNumber).MaximumLength(50);
        RuleFor(x => x.Nationality).MaximumLength(100);
    }
}

public class AdminUpdateVendorOwnerCommandHandler : IRequestHandler<AdminUpdateVendorOwnerCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminUpdateVendorOwnerCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IIdentityAccountService identityAccountService,
        IUnitOfWork unitOfWork)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _identityAccountService = identityAccountService;
        _unitOfWork = unitOfWork;
    }

    public async Task<VendorDetailDto> Handle(AdminUpdateVendorOwnerCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.UpdateOwner(request.OwnerName, request.OwnerEmail, request.OwnerPhone, request.IdNumber, request.Nationality);

        var identityResult = await _identityAccountService.UpdateProfileAsync(
            vendor.UserId,
            request.OwnerName,
            request.OwnerEmail,
            request.OwnerPhone,
            cancellationToken);

        if (!identityResult.Succeeded)
        {
            throw new BusinessRuleException("IDENTITY_UPDATE_FAILED", string.Join(", ", identityResult.Errors ?? []));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
