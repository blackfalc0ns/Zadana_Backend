using MediatR;
using FluentValidation.Results;
using Zadana.Application.Common.Exceptions;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.RejectVendorDocumentReview;

public record RejectVendorDocumentReviewCommand(Guid VendorId, string DocumentId, string Reason) : IRequest<VendorDetailDto>;

public class RejectVendorDocumentReviewCommandHandler : IRequestHandler<RejectVendorDocumentReviewCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorReadService _vendorReadService;

    public RejectVendorDocumentReviewCommandHandler(
        IVendorRepository vendorRepository,
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IIdentityAccountService identityAccountService,
        IVendorReviewAuditService vendorReviewAuditService,
        IVendorReadService vendorReadService)
    {
        _vendorRepository = vendorRepository;
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _identityAccountService = identityAccountService;
        _vendorReviewAuditService = vendorReviewAuditService;
        _vendorReadService = vendorReadService;
    }

    public async Task<VendorDetailDto> Handle(RejectVendorDocumentReviewCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure("Reason", "A rejection reason is required.")
            });
        }

        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        var documentType = ParseDocumentType(request.DocumentId);
        var reviewerName = await ResolveReviewerNameAsync(cancellationToken);

        var review = vendor.DocumentReviews.FirstOrDefault(item => item.Type == documentType);
        if (review is null)
        {
            review = new VendorDocumentReview(vendor.Id, documentType);
            _dbContext.VendorDocumentReviews.Add(review);
        }

        review.Reject(request.Reason, _currentUserService.UserId, reviewerName);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _vendorReviewAuditService.AppendEntryAsync(
            vendor.UserId,
            "document-rejected",
            "warning",
            $"{documentType} document rejected. {request.Reason.Trim()}",
            "Document Review",
            "Vendor Compliance Desk",
            _currentUserService.UserId,
            reviewerName,
            cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }

    private VendorDocumentType ParseDocumentType(string documentId) =>
        Enum.TryParse<VendorDocumentType>(documentId, true, out var parsed)
            ? parsed
            : throw new NotFoundException("VendorDocument", documentId);

    private async Task<string> ResolveReviewerNameAsync(CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return "Vendor Compliance Desk";
        }

        var actor = await _identityAccountService.FindByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        return string.IsNullOrWhiteSpace(actor?.FullName) ? "Vendor Compliance Desk" : actor.FullName;
    }
}
