using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.StartVendorReview;

public record StartVendorReviewCommand(Guid VendorId) : IRequest<VendorDetailDto>;

public class StartVendorReviewCommandHandler : IRequestHandler<StartVendorReviewCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IVendorReadService _vendorReadService;
    private readonly ICurrentUserService _currentUserService;

    public StartVendorReviewCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReviewAuditService vendorReviewAuditService,
        IVendorCommunicationService vendorCommunicationService,
        IVendorReadService vendorReadService,
        ICurrentUserService currentUserService)
    {
        _vendorRepository = vendorRepository;
        _vendorReviewAuditService = vendorReviewAuditService;
        _vendorCommunicationService = vendorCommunicationService;
        _vendorReadService = vendorReadService;
        _currentUserService = currentUserService;
    }

    public async Task<VendorDetailDto> Handle(StartVendorReviewCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
        VendorReviewWorkflow.EnsureComplianceActionAllowed(vendor);

        await _vendorReviewAuditService.AppendEntryAsync(
            vendor.UserId,
            "start-review",
            "info",
            "Vendor review started.",
            "Compliance Review",
            "Vendor Compliance Desk",
            _currentUserService.UserId,
            cancellationToken: cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_review_started",
                "بدأت مراجعة حساب التاجر",
                "Vendor review started",
                "بدأ فريق الامتثال مراجعة بياناتك ومستنداتك. سنخبرك بأي تحديث مطلوب.",
                "The compliance team started reviewing your profile and documents. We will notify you if anything is required.",
                "/profile",
                vendor.Id,
                SendPush: true),
            cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
