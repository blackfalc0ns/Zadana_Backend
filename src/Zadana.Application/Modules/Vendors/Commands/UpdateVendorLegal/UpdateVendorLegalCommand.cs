using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorLegal;

public record UpdateVendorLegalCommand(
    string CommercialRegistrationNumber,
    DateTime? CommercialRegistrationExpiryDate,
    string? TaxId,
    string? LicenseNumber,
    string? CommercialRegisterDocumentUrl) : IRequest<VendorWorkspaceDto>;

public class UpdateVendorLegalCommandValidator : AbstractValidator<UpdateVendorLegalCommand>
{
    public UpdateVendorLegalCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.CommercialRegistrationNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TaxId).MaximumLength(50);
        RuleFor(x => x.LicenseNumber).MaximumLength(100);
    }
}

public class UpdateVendorLegalCommandHandler : IRequestHandler<UpdateVendorLegalCommand, VendorWorkspaceDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateVendorLegalCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<VendorWorkspaceDto> Handle(UpdateVendorLegalCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        vendor.UpdateLegal(
            request.CommercialRegistrationNumber,
            request.CommercialRegistrationExpiryDate,
            request.TaxId,
            request.LicenseNumber,
            request.CommercialRegisterDocumentUrl);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }
}
