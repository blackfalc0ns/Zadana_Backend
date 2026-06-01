using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;
using ValidationException = Zadana.Application.Common.Exceptions.ValidationException;


namespace Zadana.Application.Modules.Vendors.Commands.ReviewVendorProfileFields;

public record ReviewVendorProfileFieldsCommand(
    Guid VendorId,
    IReadOnlyCollection<ReviewVendorProfileFieldItem> Items) : IRequest<VendorDetailDto>;

public record ReviewVendorProfileFieldItem(
    string Code,
    string Decision,
    string? Reason);

public class ReviewVendorProfileFieldsCommandValidator : AbstractValidator<ReviewVendorProfileFieldsCommand>
{
    public ReviewVendorProfileFieldsCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
    }
}

public class ReviewVendorProfileFieldsCommandHandler : IRequestHandler<ReviewVendorProfileFieldsCommand, VendorDetailDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IVendorReadService _vendorReadService;

    public ReviewVendorProfileFieldsCommandHandler(
        IVendorRepository vendorRepository,
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IIdentityAccountService identityAccountService,
        IVendorReviewAuditService vendorReviewAuditService,
        IVendorCommunicationService vendorCommunicationService,
        IVendorReadService vendorReadService)
    {
        _vendorRepository = vendorRepository;
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _identityAccountService = identityAccountService;
        _vendorReviewAuditService = vendorReviewAuditService;
        _vendorCommunicationService = vendorCommunicationService;
        _vendorReadService = vendorReadService;
    }

    public async Task<VendorDetailDto> Handle(ReviewVendorProfileFieldsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
                ?? throw new NotFoundException("Vendor", request.VendorId);

            var existingReviewItemIds = vendor.ProfileReviewItems.Select(x => x.Id).ToHashSet();

            var reviewerName = await ResolveReviewerNameAsync(cancellationToken);
            var normalizedItems = request.Items
                .Select(item => new
                {
                    Code = item.Code.Trim(),
                    Decision = item.Decision.Trim().ToLowerInvariant(),
                    Reason = item.Reason?.Trim()
                })
                .ToList();

            foreach (var item in normalizedItems)
            {
                if (!VendorProfileReviewCatalog.TryGetDefinition(item.Code, out var definition))
                {
                    throw new ValidationException(new[]
                    {
                        new ValidationFailure("items.code", $"Unknown review code '{item.Code}'.")
                    });
                }

                var review = VendorProfileReviewMutations.GetOrCreate(vendor, item.Code);
                switch (item.Decision)
                {
                    case "approved":
                        review.Approve(_currentUserService.UserId, reviewerName);
                        break;
                    case "rejected":
                        if (string.IsNullOrWhiteSpace(item.Reason))
                        {
                            throw new ValidationException(new[]
                            {
                                new ValidationFailure("items.reason", $"A rejection reason is required for '{item.Code}'.")
                            });
                        }

                        review.Reject(item.Reason, _currentUserService.UserId, reviewerName);
                        break;
                    default:
                        throw new ValidationException(new[]
                        {
                            new ValidationFailure("items.decision", $"Unsupported decision '{item.Decision}'.")
                        });
                }

                var actionLabel = item.Decision == "approved" ? "قبول" : "رفض";
                AddAuditNotifications(
                    vendor.UserId,
                    item.Decision == "approved" ? "profile-field-approved" : "profile-field-rejected",
                    item.Decision == "approved" ? "success" : "warning",
                    item.Decision == "approved"
                        ? $"تم {actionLabel} العنصر {item.Code}."
                        : $"تم {actionLabel} العنصر {item.Code}. {item.Reason}",
                    "مراجعة بيانات التاجر",
                    reviewerName);
            }
            var efContext = _dbContext as Microsoft.EntityFrameworkCore.DbContext;
            if (efContext != null)
            {
                foreach (var reviewItem in vendor.ProfileReviewItems)
                {
                    if (!existingReviewItemIds.Contains(reviewItem.Id))
                    {
                        efContext.Entry(reviewItem).State = Microsoft.EntityFrameworkCore.EntityState.Added;
                    }
                }
            }
            await _dbContext.SaveChangesAsync(cancellationToken);

            var rejectedCount = normalizedItems.Count(item => item.Decision == "rejected");
            await _vendorCommunicationService.SendAsync(
                vendor,
                new VendorCommunicationMessage(
                    rejectedCount > 0 ? "vendor_profile_changes_requested" : "vendor_profile_items_approved",
                    rejectedCount > 0 ? "مطلوب تعديل بيانات في ملف التاجر" : "تم اعتماد عناصر من ملف التاجر",
                    rejectedCount > 0 ? "Vendor profile changes requested" : "Vendor profile items approved",
                    rejectedCount > 0
                        ? "تمت مراجعة بعض بيانات الملف وتحتاج إلى تعديل قبل إعادة الإرسال."
                        : "تم اعتماد بعض عناصر ملف التاجر من فريق الامتثال.",
                    rejectedCount > 0
                        ? "Some profile items were reviewed and need correction before resubmission."
                        : "Some vendor profile items were approved by the compliance team.",
                    "/profile",
                    vendor.Id,
                    SendPush: true),
                cancellationToken);

            return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
                ?? throw new NotFoundException("Vendor", request.VendorId);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            var details = string.Join(", ", ex.Entries.Select(e => $"{e.Entity.GetType().Name} (State: {e.State})"));
            throw new Exception($"Concurrency exception details: {details}", ex);
        }
    }

    private async Task<string> ResolveReviewerNameAsync(CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return "Vendor Compliance Desk";
        }

        var actor = await _identityAccountService.FindByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        return string.IsNullOrWhiteSpace(actor?.FullName) ? "Vendor Compliance Desk" : actor.FullName;
    }

    private void AddAuditNotifications(
        Guid vendorUserId,
        string kind,
        string tone,
        string message,
        string roleLabel,
        string reviewerName)
    {
        string BuildType(string prefix)
        {
            static string NormalizePart(string? value, string fallback) =>
                string.IsNullOrWhiteSpace(value)
                    ? fallback
                    : value.Trim().Replace('|', '/');

            return $"{prefix}|{NormalizePart(kind, "note")}|{NormalizePart(tone, "info")}|{NormalizePart(roleLabel, "Vendor Review")}";
        }

        var notif1 = new Zadana.Domain.Modules.Social.Entities.Notification(
            vendorUserId,
            reviewerName,
            reviewerName,
            message,
            message,
            BuildType("vendor-review"));

        var notif2 = new Zadana.Domain.Modules.Social.Entities.Notification(
            vendorUserId,
            reviewerName,
            reviewerName,
            message,
            message,
            BuildType("vendor-activity"));

        _dbContext.Notifications.Add(notif1);
        _dbContext.Notifications.Add(notif2);
    }
}
