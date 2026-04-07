using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.RejectVendor;

public class RejectVendorCommandHandler : IRequestHandler<RejectVendorCommand>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RejectVendorCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReviewAuditService vendorReviewAuditService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _vendorRepository = vendorRepository;
        _vendorReviewAuditService = vendorReviewAuditService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task Handle(RejectVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.Reject(request.Reason);

        await _vendorReviewAuditService.AppendEntryAsync(
            vendor.UserId,
            "rejected",
            "danger",
            request.Reason,
            "Compliance Review",
            "Vendor Compliance Desk",
            _currentUserService.UserId,
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
