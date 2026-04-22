using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.RequestVendorDocuments;

public record RequestVendorDocumentsCommand(Guid VendorId, string Note) : IRequest<VendorDetailDto>;

public class RequestVendorDocumentsCommandHandler : IRequestHandler<RequestVendorDocumentsCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorReadService _vendorReadService;
    private readonly ICurrentUserService _currentUserService;

    public RequestVendorDocumentsCommandHandler(
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

    public async Task<VendorDetailDto> Handle(RequestVendorDocumentsCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
        VendorReviewWorkflow.EnsureComplianceActionAllowed(vendor);

        var note = string.IsNullOrWhiteSpace(request.Note)
            ? "Please re-upload the required legal documents and confirm the latest vendor information."
            : request.Note.Trim();

        await _vendorReviewAuditService.AppendEntryAsync(
            vendor.UserId,
            "request-documents",
            "warning",
            note,
            "Compliance Review",
            "Vendor Compliance Desk",
            _currentUserService.UserId,
            cancellationToken: cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
