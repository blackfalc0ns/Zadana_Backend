using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.ArchiveVendor;

public record ArchiveVendorCommand(Guid VendorId, string Reason) : IRequest;

public class ArchiveVendorCommandValidator : AbstractValidator<ArchiveVendorCommand>
{
    public ArchiveVendorCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class ArchiveVendorCommandHandler : IRequestHandler<ArchiveVendorCommand>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ArchiveVendorCommandHandler(
        IVendorRepository vendorRepository,
        IIdentityAccountService identityAccountService,
        IRefreshTokenStore refreshTokenStore,
        IVendorReviewAuditService vendorReviewAuditService,
        IVendorCommunicationService vendorCommunicationService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _vendorRepository = vendorRepository;
        _identityAccountService = identityAccountService;
        _refreshTokenStore = refreshTokenStore;
        _vendorReviewAuditService = vendorReviewAuditService;
        _vendorCommunicationService = vendorCommunicationService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task Handle(ArchiveVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.Archive(request.Reason);

        var archiveResult = await _identityAccountService.ArchiveAsync(vendor.UserId, request.Reason, cancellationToken);
        if (!archiveResult.Succeeded)
        {
            throw new BusinessRuleException("IDENTITY_ARCHIVE_FAILED", string.Join(", ", archiveResult.Errors ?? []));
        }

        await _refreshTokenStore.RevokeAllByUserAsync(vendor.UserId, cancellationToken);

        await _vendorReviewAuditService.AppendEntryAsync(
            vendor.UserId,
            "archived",
            "danger",
            request.Reason,
            "Admin Action",
            "Admin",
            _currentUserService.UserId,
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_archived",
                "تمت أرشفة حساب التاجر",
                "Vendor account archived",
                request.Reason,
                request.Reason,
                "/alerts",
                vendor.Id,
                SendPush: true),
            cancellationToken);
    }
}
