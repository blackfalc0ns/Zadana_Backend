using FluentValidation;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorOperationsSettings;

public record UpdateVendorOperationsSettingsCommand(
    bool AcceptOrders,
    decimal? MinimumOrderAmount,
    int? PreparationTimeMinutes) : IRequest<VendorWorkspaceDto>;

public class UpdateVendorOperationsSettingsCommandValidator : AbstractValidator<UpdateVendorOperationsSettingsCommand>
{
    public UpdateVendorOperationsSettingsCommandValidator()
    {
        RuleFor(x => x.MinimumOrderAmount).GreaterThanOrEqualTo(0).When(x => x.MinimumOrderAmount.HasValue);
        RuleFor(x => x.PreparationTimeMinutes).GreaterThanOrEqualTo(0).When(x => x.PreparationTimeMinutes.HasValue);
    }
}

public class UpdateVendorOperationsSettingsCommandHandler : IRequestHandler<UpdateVendorOperationsSettingsCommand, VendorWorkspaceDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateVendorOperationsSettingsCommandHandler(
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

    public async Task<VendorWorkspaceDto> Handle(UpdateVendorOperationsSettingsCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        vendor.UpdateOperationsSettings(request.AcceptOrders, request.MinimumOrderAmount, request.PreparationTimeMinutes);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }
}
