using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UnlockVendorLogin;

public record UnlockVendorLoginCommand(Guid VendorId) : IRequest;

public class UnlockVendorLoginCommandValidator : AbstractValidator<UnlockVendorLoginCommand>
{
    public UnlockVendorLoginCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty();
    }
}

public class UnlockVendorLoginCommandHandler : IRequestHandler<UnlockVendorLoginCommand>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IUnitOfWork _unitOfWork;

    public UnlockVendorLoginCommandHandler(
        IVendorRepository vendorRepository,
        IIdentityAccountService identityAccountService,
        IUnitOfWork unitOfWork)
    {
        _vendorRepository = vendorRepository;
        _identityAccountService = identityAccountService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UnlockVendorLoginCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.Unlock();

        var unlockResult = await _identityAccountService.UnlockLoginAsync(vendor.UserId, cancellationToken);
        if (!unlockResult.Succeeded)
        {
            throw new BusinessRuleException("IDENTITY_UNLOCK_FAILED", string.Join(", ", unlockResult.Errors ?? []));
        }

        var activateResult = await _identityAccountService.ActivateAsync(vendor.UserId, cancellationToken);
        if (!activateResult.Succeeded)
        {
            throw new BusinessRuleException("IDENTITY_ACTIVATE_FAILED", string.Join(", ", activateResult.Errors ?? []));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
