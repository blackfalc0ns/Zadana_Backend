using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.ReopenVendorReview;

public record ReopenVendorReviewCommand(Guid VendorId) : IRequest<VendorDetailDto>;

public class ReopenVendorReviewCommandHandler : IRequestHandler<ReopenVendorReviewCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ReopenVendorReviewCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IVendorReviewAuditService vendorReviewAuditService,
        IVendorCommunicationService vendorCommunicationService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _vendorReviewAuditService = vendorReviewAuditService;
        _vendorCommunicationService = vendorCommunicationService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<VendorDetailDto> Handle(ReopenVendorReviewCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.ReopenForReview();

        await _vendorReviewAuditService.AppendEntryAsync(
            vendor.UserId,
            "reopen-review",
            "info",
            "تم فتح ملف اعتماد التاجر للمراجعة مرة أخرى.",
            "مراجعة الامتثال",
            "المسؤول",
            _currentUserService.UserId,
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_review_reopened",
                "تم فتح ملف اعتماد التاجر",
                "Vendor review reopened",
                "تم فتح ملف اعتماد متجرك للمراجعة مرة أخرى. ستصلك أي تحديثات مطلوبة من فريق الامتثال.",
                "Your vendor approval file has been reopened for review. The compliance team will share any required updates.",
                "/profile",
                vendor.Id,
                SendPush: true),
            cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
