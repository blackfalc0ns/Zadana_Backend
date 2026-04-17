using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Extensions;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.BrandRequests.ReviewRequest;

public class ReviewBrandRequestCommandHandler : IRequestHandler<ReviewBrandRequestCommand, Guid?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ReviewBrandRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdentityAccountService identityAccountService,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityAccountService = identityAccountService;
        _localizer = localizer;
    }

    public async Task<Guid?> Handle(ReviewBrandRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.HasRole(UserRole.Admin, UserRole.SuperAdmin))
        {
            throw new ForbiddenAccessException(_localizer["UNAUTHORIZED_REVIEW_REQUESTS"]);
        }

        var brandRequest = await _context.BrandRequests
            .Include(x => x.Vendor)
            .FirstOrDefaultAsync(x => x.Id == request.BrandRequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(BrandRequest), request.BrandRequestId);

        if (brandRequest.Status != ApprovalStatus.Pending)
        {
            throw new BusinessRuleException("REQUEST_ALREADY_REVIEWED", _localizer["REQUEST_ALREADY_REVIEWED"]);
        }

        var reviewerName = await ResolveReviewerNameAsync(cancellationToken);

        if (request.IsApproved)
        {
            var brand = new Brand(brandRequest.NameAr, brandRequest.NameEn, brandRequest.LogoUrl, brandRequest.CategoryId);
            _context.Brands.Add(brand);
            brandRequest.Approve(reviewerName, brand.Id);
            _context.Notifications.Add(new Notification(
                brandRequest.Vendor.UserId,
                "تمت الموافقة على طلب الكتالوج",
                "Catalog Request Approved",
                $"تمت الموافقة على طلب العلامة التجارية '{brandRequest.NameAr}'.",
                $"Your brand request '{brandRequest.NameEn}' has been approved.",
                "catalog_request_brand"));
            await _context.SaveChangesAsync(cancellationToken);
            return brand.Id;
        }

        if (string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            throw new BadRequestException("REJECTION_REASON_REQUIRED", _localizer["REJECTION_REASON_REQUIRED"]);
        }

        brandRequest.Reject(request.RejectionReason, reviewerName);
        _context.Notifications.Add(new Notification(
            brandRequest.Vendor.UserId,
            "تم رفض طلب الكتالوج",
            "Catalog Request Rejected",
            $"تم رفض طلب العلامة التجارية '{brandRequest.NameAr}'. السبب: {request.RejectionReason}",
            $"Your brand request '{brandRequest.NameEn}' was rejected. Reason: {request.RejectionReason}",
            "catalog_request_brand"));
        await _context.SaveChangesAsync(cancellationToken);
        return null;
    }

    private async Task<string> ResolveReviewerNameAsync(CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return "Admin";
        }

        var reviewer = await _identityAccountService.FindByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        return string.IsNullOrWhiteSpace(reviewer?.FullName) ? "Admin" : reviewer.FullName;
    }
}
