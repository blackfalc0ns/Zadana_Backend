using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.LockVendorLogin;

public record LockVendorLoginCommand(Guid VendorId, string Reason) : IRequest;

public class LockVendorLoginCommandValidator : AbstractValidator<LockVendorLoginCommand>
{
    public LockVendorLoginCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class LockVendorLoginCommandHandler : IRequestHandler<LockVendorLoginCommand>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IUnitOfWork _unitOfWork;

    public LockVendorLoginCommandHandler(
        IVendorRepository vendorRepository,
        IIdentityAccountService identityAccountService,
        IRefreshTokenStore refreshTokenStore,
        IUnitOfWork unitOfWork)
    {
        _vendorRepository = vendorRepository;
        _identityAccountService = identityAccountService;
        _refreshTokenStore = refreshTokenStore;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(LockVendorLoginCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.Lock(request.Reason);

        var lockResult = await _identityAccountService.LockLoginAsync(vendor.UserId, request.Reason, cancellationToken);
        if (!lockResult.Succeeded)
        {
            throw new BusinessRuleException("IDENTITY_LOCK_FAILED", string.Join(", ", lockResult.Errors ?? []));
        }

        await _refreshTokenStore.RevokeAllByUserAsync(vendor.UserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
