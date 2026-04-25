using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorLegal;

public record UpdateVendorLegalCommand(
    string CommercialRegistrationNumber,
    DateTime? CommercialRegistrationExpiryDate,
    string? TaxId,
    string? LicenseNumber,
    string? CommercialRegisterDocumentUrl,
    string? TaxDocumentUrl,
    string? LicenseDocumentUrl) : IRequest<VendorWorkspaceDto>;

public class UpdateVendorLegalCommandValidator : AbstractValidator<UpdateVendorLegalCommand>
{
    public UpdateVendorLegalCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.CommercialRegistrationNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TaxId).MaximumLength(50);
        RuleFor(x => x.LicenseNumber).MaximumLength(100);
    }
}

public class UpdateVendorLegalCommandHandler : IRequestHandler<UpdateVendorLegalCommand, VendorWorkspaceDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;

    public UpdateVendorLegalCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IVendorReviewAuditService vendorReviewAuditService)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _vendorReviewAuditService = vendorReviewAuditService;
    }

    public async Task<VendorWorkspaceDto> Handle(UpdateVendorLegalCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        var resetDocuments = ResolveReuploadedRejectedDocuments(
            vendor.CommercialRegisterDocumentUrl,
            request.CommercialRegisterDocumentUrl,
            vendor.TaxDocumentUrl,
            request.TaxDocumentUrl,
            vendor.LicenseDocumentUrl,
            request.LicenseDocumentUrl);

        vendor.UpdateLegal(
            request.CommercialRegistrationNumber,
            request.CommercialRegistrationExpiryDate,
            request.TaxId,
            request.LicenseNumber,
            request.CommercialRegisterDocumentUrl,
            request.TaxDocumentUrl,
            request.LicenseDocumentUrl);

        foreach (var documentType in resetDocuments)
        {
            var review = vendor.DocumentReviews.FirstOrDefault(item => item.Type == documentType);
            if (review?.Decision == VendorDocumentReviewDecision.Rejected)
            {
                review.ResetToPending();
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (resetDocuments.Count > 0)
        {
            await _vendorReviewAuditService.AppendEntryAsync(
                vendor.UserId,
                "vendor-document-reuploaded",
                "info",
                $"Vendor re-uploaded document(s): {string.Join(", ", resetDocuments)}. They are back in the review queue.",
                "Vendor Portal",
                vendor.BusinessNameEn,
                userId,
                vendor.BusinessNameEn,
                cancellationToken);
        }
        else
        {
            await _vendorReviewAuditService.AppendActivityEntryAsync(
                vendor.UserId,
                "profile-legal-updated",
                "warning",
                "Vendor updated legal and compliance information from Vendor Portal.",
                "Vendor Portal",
                vendor.BusinessNameEn,
                userId,
                vendor.BusinessNameEn,
                cancellationToken);
        }

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }

    private static IReadOnlyList<VendorDocumentType> ResolveReuploadedRejectedDocuments(
        string? currentCommercialUrl,
        string? nextCommercialUrl,
        string? currentTaxUrl,
        string? nextTaxUrl,
        string? currentLicenseUrl,
        string? nextLicenseUrl)
    {
        var changed = new List<VendorDocumentType>();

        if (HasChanged(currentCommercialUrl, nextCommercialUrl))
        {
            changed.Add(VendorDocumentType.Commercial);
        }

        if (HasChanged(currentTaxUrl, nextTaxUrl))
        {
            changed.Add(VendorDocumentType.Tax);
        }

        if (HasChanged(currentLicenseUrl, nextLicenseUrl))
        {
            changed.Add(VendorDocumentType.License);
        }

        return changed;
    }

    private static bool HasChanged(string? currentValue, string? nextValue) =>
        !string.IsNullOrWhiteSpace(nextValue)
        && !string.Equals(currentValue?.Trim(), nextValue.Trim(), StringComparison.OrdinalIgnoreCase);
}
