using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorContact;

public record UpdateVendorContactCommand(
    string Region,
    string City,
    string NationalAddress) : IRequest<VendorWorkspaceDto>;

public class UpdateVendorContactCommandValidator : AbstractValidator<UpdateVendorContactCommand>
{
    public UpdateVendorContactCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Region).NotEmpty().MaximumLength(100);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NationalAddress).NotEmpty().MaximumLength(500);
    }
}

public class UpdateVendorContactCommandHandler : IRequestHandler<UpdateVendorContactCommand, VendorWorkspaceDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;

    public UpdateVendorContactCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IVendorReviewAuditService vendorReviewAuditService)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _vendorReviewAuditService = vendorReviewAuditService;
    }

    public async Task<VendorWorkspaceDto> Handle(UpdateVendorContactCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        vendor.UpdateContact(request.Region, request.City, request.NationalAddress);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorReviewAuditService.AppendActivityEntryAsync(
            vendor.UserId,
            "profile-contact-updated",
            "info",
            "Vendor updated address and contact location details from Vendor Portal.",
            "Vendor Portal",
            vendor.BusinessNameEn,
            userId,
            vendor.BusinessNameEn,
            cancellationToken);

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }
}
