using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AddVendorReviewNote;

public record AddVendorReviewNoteCommand(
    Guid VendorId,
    string Message,
    string? AuthorName,
    string? RoleLabel) : IRequest<VendorDetailDto>;

public class AddVendorReviewNoteCommandHandler : IRequestHandler<AddVendorReviewNoteCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorReadService _vendorReadService;
    private readonly ICurrentUserService _currentUserService;

    public AddVendorReviewNoteCommandHandler(
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

    public async Task<VendorDetailDto> Handle(AddVendorReviewNoteCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        var roleLabel = string.IsNullOrWhiteSpace(request.RoleLabel)
            ? "Vendor Review"
            : request.RoleLabel.Trim();

        await _vendorReviewAuditService.AppendEntryAsync(
            vendor.UserId,
            "note",
            "info",
            request.Message,
            roleLabel,
            "Operations Reviewer",
            _currentUserService.UserId,
            request.AuthorName,
            cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }
}
