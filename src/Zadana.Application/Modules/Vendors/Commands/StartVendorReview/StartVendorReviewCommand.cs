using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.StartVendorReview;

public record StartVendorReviewCommand(Guid VendorId) : IRequest<VendorDetailDto>;

public class StartVendorReviewCommandHandler : IRequestHandler<StartVendorReviewCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorReadService _vendorReadService;
    private readonly ICurrentUserService _currentUserService;

    public StartVendorReviewCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReviewAuditService vendorReviewAuditService,
        IVendorReadService vendorReadService,
        ICurrentUserService currentUserService)
    {
        _vendorRepository = vendorRepository;
        _vendorReviewAuditService = vendorReviewAuditService;
        _vendorReadService = vendorReadService;
        _currentUserService = currentUserService;
    }

    public async Task<VendorDetailDto> Handle(StartVendorReviewCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        await _vendorReviewAuditService.AppendEntryAsync(
            vendor.UserId,
            "start-review",
            "info",
            "Vendor review started.",
            "Compliance Review",
            "Vendor Compliance Desk",
            _currentUserService.UserId,
            cancellationToken: cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
