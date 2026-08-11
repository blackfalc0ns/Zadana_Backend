using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.RequestVendorDocuments;

public record RequestVendorDocumentsCommand(Guid VendorId, string DocumentId, string Note) : IRequest<VendorDetailDto>;

public class RequestVendorDocumentsCommandHandler : IRequestHandler<RequestVendorDocumentsCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IVendorReadService _vendorReadService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public RequestVendorDocumentsCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReviewAuditService vendorReviewAuditService,
        IVendorCommunicationService vendorCommunicationService,
        IVendorReadService vendorReadService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _vendorRepository = vendorRepository;
        _vendorReviewAuditService = vendorReviewAuditService;
        _vendorCommunicationService = vendorCommunicationService;
        _vendorReadService = vendorReadService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<VendorDetailDto> Handle(RequestVendorDocumentsCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
        VendorReviewWorkflow.EnsureComplianceActionAllowed(vendor);

        if (!Enum.TryParse<VendorDocumentType>(request.DocumentId, true, out var documentType)
            || !VendorReviewWorkflow.IsRequired(documentType))
        {
            throw new BusinessRuleException(
                "VendorReviewInvalidCorrectionDocument",
                "A valid required vendor document must be selected for re-upload.");
        }

        VendorReviewWorkflow.EnsureDocumentCanBeReviewed(vendor, documentType);

        var note = string.IsNullOrWhiteSpace(request.Note)
            ? "فضلاً إعادة رفع المستندات القانونية المطلوبة وتأكيد أحدث بيانات التاجر."
            : request.Note.Trim();

        var review = vendor.DocumentReviews.FirstOrDefault(item => item.Type == documentType);
        if (review is null)
        {
            review = new VendorDocumentReview(vendor.Id, documentType);
            vendor.DocumentReviews.Add(review);
        }

        review.Reject(note, _currentUserService.UserId, "Vendor Compliance Desk");
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorReviewAuditService.AppendEntryAsync(
            vendor.UserId,
            "request-documents",
            "warning",
            note,
            "مراجعة الامتثال",
            "مكتب امتثال التاجر",
            _currentUserService.UserId,
            cancellationToken: cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_documents_requested",
                "مطلوب تحديث مستندات التاجر",
                "Vendor documents require updates",
                note,
                note,
                "/profile",
                vendor.Id,
                SendPush: true),
            cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
