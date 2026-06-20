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
            }

            var sectionNotifications = normalizedItems
                .Where(item => VendorProfileReviewCatalog.TryResolveSection(item.Code, out _))
                .GroupBy(item =>
                {
                    VendorProfileReviewCatalog.TryResolveSection(item.Code, out var section);
                    return section;
                })
                .ToList();

            foreach (var group in sectionNotifications)
            {
                var (labelAr, labelEn) = VendorProfileReviewCatalog.GetSectionLabel(group.Key);
                var hasRejected = group.Any(item => item.Decision == "rejected");
                var rejectionReasons = group
                    .Where(item => item.Decision == "rejected" && !string.IsNullOrWhiteSpace(item.Reason))
                    .Select(item => item.Reason!)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                var messageAr = !hasRejected
                    ? $"تم قبول قسم {labelAr}."
                    : rejectionReasons.Count switch
                    {
                        0 => $"تم طلب تعديلات على قسم {labelAr}.",
                        1 => $"تم طلب تعديلات على قسم {labelAr}: {rejectionReasons[0]}",
                        _ => $"تم طلب تعديلات على قسم {labelAr}: {string.Join(" • ", rejectionReasons)}"
                    };

                var messageEn = !hasRejected
                    ? $"{labelEn} section was approved."
                    : rejectionReasons.Count switch
                    {
                        0 => $"Changes were requested for the {labelEn} section.",
                        1 => $"Changes were requested for the {labelEn} section: {rejectionReasons[0]}",
                        _ => $"Changes were requested for the {labelEn} section: {string.Join(" • ", rejectionReasons)}"
                    };

                AddAuditNotifications(
                    vendor.UserId,
                    $"profile-section-{group.Key}-{(hasRejected ? "rejected" : "approved")}",
                    hasRejected ? "warning" : "success",
                    messageAr,
                    messageEn,
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
            var affectedSections = normalizedItems
                .Select(item =>
                {
                    return VendorProfileReviewCatalog.TryResolveSection(item.Code, out var section)
                        ? section
                        : null;
                })
                .Where(section => !string.IsNullOrWhiteSpace(section))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList();

            var targetUrl = affectedSections.Count == 1
                ? $"/profile?tab={VendorProfileReviewCatalog.BuildProfileSectionTab(affectedSections[0])}"
                : "/profile";

            await _vendorCommunicationService.SendAsync(
                vendor,
                new VendorCommunicationMessage(
                    rejectedCount > 0 ? "vendor_profile_changes_requested" : "vendor_profile_items_approved",
                    rejectedCount > 0 ? "مطلوب تعديل بيانات في ملف التاجر" : "تم اعتماد عناصر من ملف التاجر",
                    rejectedCount > 0 ? "Vendor profile changes requested" : "Vendor profile items approved",
                    rejectedCount > 0
                        ? BuildSectionAwareBodyAr(affectedSections, changesRequested: true)
                        : BuildSectionAwareBodyAr(affectedSections, changesRequested: false),
                    rejectedCount > 0
                        ? BuildSectionAwareBodyEn(affectedSections, changesRequested: true)
                        : BuildSectionAwareBodyEn(affectedSections, changesRequested: false),
                    targetUrl,
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

    private static string BuildSectionAwareBodyAr(IReadOnlyCollection<string> sections, bool changesRequested)
    {
        if (sections.Count == 0)
        {
            return changesRequested
                ? "تمت مراجعة بعض بيانات الملف وتحتاج إلى تعديل قبل إعادة الإرسال."
                : "تم اعتماد بعض عناصر ملف التاجر من فريق الامتثال.";
        }

        var labels = sections.Select(section => VendorProfileReviewCatalog.GetSectionLabel(section).LabelAr).ToList();
        var sectionList = string.Join("، ", labels);

        return changesRequested
            ? $"تمت مراجعة {sectionList} وتحتاج إلى تعديل قبل إعادة الإرسال."
            : $"تم اعتماد {sectionList} من فريق الامتثال.";
    }

    private static string BuildSectionAwareBodyEn(IReadOnlyCollection<string> sections, bool changesRequested)
    {
        if (sections.Count == 0)
        {
            return changesRequested
                ? "Some profile items were reviewed and need correction before resubmission."
                : "Some vendor profile items were approved by the compliance team.";
        }

        var labels = sections.Select(section => VendorProfileReviewCatalog.GetSectionLabel(section).LabelEn).ToList();
        var sectionList = string.Join(", ", labels);

        return changesRequested
            ? $"{sectionList} were reviewed and need correction before resubmission."
            : $"{sectionList} were approved by the compliance team.";
    }

    private void AddAuditNotifications(
        Guid vendorUserId,
        string kind,
        string tone,
        string messageAr,
        string messageEn,
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
            messageAr,
            messageEn,
            BuildType("vendor-review"));

        var notif2 = new Zadana.Domain.Modules.Social.Entities.Notification(
            vendorUserId,
            reviewerName,
            reviewerName,
            messageAr,
            messageEn,
            BuildType("vendor-activity"));

        _dbContext.Notifications.Add(notif1);
        _dbContext.Notifications.Add(notif2);
    }
}
