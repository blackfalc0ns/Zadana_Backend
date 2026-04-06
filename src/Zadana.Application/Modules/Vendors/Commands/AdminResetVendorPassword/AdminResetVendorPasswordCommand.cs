using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AdminResetVendorPassword;

public record AdminResetVendorPasswordCommand(Guid VendorId, string NewPassword) : IRequest;

public class AdminResetVendorPasswordCommandValidator : AbstractValidator<AdminResetVendorPasswordCommand>
{
    public AdminResetVendorPasswordCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

public class AdminResetVendorPasswordCommandHandler : IRequestHandler<AdminResetVendorPasswordCommand>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IUnitOfWork _unitOfWork;

    public AdminResetVendorPasswordCommandHandler(
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

    public async Task Handle(AdminResetVendorPasswordCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        var resetResult = await _identityAccountService.ResetPasswordByAdminAsync(vendor.UserId, request.NewPassword, cancellationToken);
        if (!resetResult.Succeeded)
        {
            throw new BusinessRuleException("IDENTITY_RESET_PASSWORD_FAILED", string.Join(", ", resetResult.Errors ?? []));
        }

        await _refreshTokenStore.RevokeAllByUserAsync(vendor.UserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
