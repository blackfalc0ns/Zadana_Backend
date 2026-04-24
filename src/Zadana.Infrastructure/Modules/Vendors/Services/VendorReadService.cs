using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Vendors.Services;

public class VendorReadService : IVendorReadService
{
    private readonly ApplicationDbContext _dbContext;

    public VendorReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<VendorListItemDto>> GetAllAsync(
        VendorStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query =
            from vendor in _dbContext.Vendors.AsNoTracking()
            join user in _dbContext.Users.AsNoTracking() on vendor.UserId equals user.Id
            select new { vendor, user };

        if (status.HasValue)
        {
            query = query.Where(item => item.vendor.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";

            query = query.Where(item =>
                EF.Functions.Like(item.vendor.BusinessNameAr, pattern) ||
                EF.Functions.Like(item.vendor.BusinessNameEn, pattern) ||
                EF.Functions.Like(item.vendor.ContactPhone, pattern) ||
                EF.Functions.Like(item.vendor.ContactEmail, pattern) ||
                EF.Functions.Like(item.user.FullName, pattern) ||
                (item.vendor.OwnerName != null && EF.Functions.Like(item.vendor.OwnerName, pattern)) ||
                (item.vendor.OwnerEmail != null && EF.Functions.Like(item.vendor.OwnerEmail, pattern)));
        }

        var projected = query
            .OrderByDescending(item => item.vendor.CreatedAtUtc)
            .Select(item => new VendorListItemDto(
                item.vendor.Id,
                item.vendor.BusinessNameAr,
                item.vendor.BusinessNameEn,
                item.vendor.BusinessType,
                NormalizeVendorStatus(item.vendor.Status),
                item.vendor.OwnerName ?? item.user.FullName,
                item.vendor.ContactPhone,
                item.vendor.CreatedAtUtc,
                item.vendor.ContactEmail,
                item.vendor.CommissionRate,
                item.vendor.City,
                item.vendor.Region,
                item.user.AccountStatus.ToString(),
                item.user.IsLoginLocked,
                item.user.LockedAtUtc,
                item.user.ArchivedAtUtc));

        return await PaginatedList<VendorListItemDto>.CreateAsync(projected, page, pageSize, cancellationToken);
    }

    public async Task<VendorDetailDto?> GetDetailAsync(Guid vendorId, CancellationToken cancellationToken = default)
    {
        var vendor = await _dbContext.Vendors
            .AsNoTracking()
            .Include(item => item.Branches)
                .ThenInclude(branch => branch.OperatingHours)
            .Include(item => item.BankAccounts)
            .Include(item => item.DocumentReviews)
            .FirstOrDefaultAsync(item => item.Id == vendorId, cancellationToken);

        if (vendor == null)
        {
            return null;
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == vendor.UserId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var approvedByName = vendor.ApprovedBy.HasValue
            ? await _dbContext.Users
                .AsNoTracking()
                .Where(item => item.Id == vendor.ApprovedBy.Value)
                .Select(item => item.FullName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var reviewNotifications = await _dbContext.Notifications
            .AsNoTracking()
            .Where(item => item.UserId == vendor.UserId && item.Type != null && item.Type.StartsWith("vendor-review|"))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return MapDetail(vendor, user, approvedByName, reviewNotifications);
    }

    public async Task<VendorActivityLogPageDto?> GetActivityLogAsync(
        Guid vendorId,
        string? type,
        string? severity,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var vendor = await _dbContext.Vendors
            .AsNoTracking()
            .Where(item => item.Id == vendorId)
            .Select(item => new { item.Id, item.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (vendor == null)
        {
            return null;
        }

        var safePage = Math.Max(page, 1);
        var safePageSize = pageSize <= 0 ? 20 : pageSize;
        var normalizedType = NormalizeActivityFilter(type);
        var normalizedSeverity = NormalizeActivityFilter(severity);
        var fromUtc = dateFrom?.ToUniversalTime();
        var toUtc = dateTo?.ToUniversalTime();

        var activityNotifications = await _dbContext.Notifications
            .AsNoTracking()
            .Where(item => item.UserId == vendor.UserId && item.Type != null && item.Type.StartsWith("vendor-activity|"))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var filtered = activityNotifications
            .Where(item =>
            {
                var meta = GetActivityMeta(item.Type);

                if (normalizedType != null && !string.Equals(meta.Kind, normalizedType, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (normalizedSeverity != null && !string.Equals(meta.Severity, normalizedSeverity, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (fromUtc.HasValue && item.CreatedAtUtc < fromUtc.Value)
                {
                    return false;
                }

                if (toUtc.HasValue && item.CreatedAtUtc > toUtc.Value)
                {
                    return false;
                }

                return true;
            })
            .ToList();

        var totalCount = filtered.Count;
        var items = filtered
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(MapActivityLogEntry)
            .ToList();

        return new VendorActivityLogPageDto(items, totalCount, safePage, safePageSize);
    }

    public async Task<VendorWorkspaceDto?> GetWorkspaceByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var vendor = await _dbContext.Vendors
            .AsNoTracking()
            .Include(item => item.Branches)
                .ThenInclude(branch => branch.OperatingHours)
            .Include(item => item.BankAccounts)
            .Include(item => item.DocumentReviews)
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (vendor == null)
        {
            return null;
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == vendor.UserId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var approvedByName = vendor.ApprovedBy.HasValue
            ? await _dbContext.Users
                .AsNoTracking()
                .Where(item => item.Id == vendor.ApprovedBy.Value)
                .Select(item => item.FullName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var reviewNotifications = await _dbContext.Notifications
            .AsNoTracking()
            .Where(item => item.UserId == vendor.UserId && item.Type != null && item.Type.StartsWith("vendor-review|"))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return MapWorkspace(vendor, user, approvedByName, reviewNotifications);
    }

    public Task<Guid?> GetVendorIdByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Vendors
            .AsNoTracking()
            .Where(vendor => vendor.UserId == userId)
            .Select(vendor => (Guid?)vendor.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static VendorWorkspaceDto MapWorkspace(
        Vendor vendor,
        User user,
        string? approvedByName,
        IReadOnlyList<Notification>? reviewNotifications = null)
    {
        var primaryBankAccount = GetPrimaryBankAccount(vendor);
        var primaryBranch = GetPrimaryBranch(vendor);
        var notifications = reviewNotifications ?? [];
        var reviewDocuments = MapReviewDocuments(vendor, MapBankAccount(primaryBankAccount), user);
        var workspaceReview = BuildWorkspaceReview(vendor, reviewDocuments, notifications);

        return new VendorWorkspaceDto(
            vendor.Id,
            vendor.BusinessNameAr,
            vendor.BusinessNameEn,
            vendor.BusinessType,
            vendor.CommercialRegistrationNumber,
            vendor.CommercialRegistrationExpiryDate,
            vendor.TaxId,
            vendor.LicenseNumber,
            vendor.ContactEmail,
            vendor.ContactPhone,
            vendor.DescriptionAr,
            vendor.DescriptionEn,
            vendor.Region,
            vendor.City,
            vendor.NationalAddress,
            vendor.OwnerName ?? user.FullName,
            vendor.OwnerEmail ?? user.Email,
            vendor.OwnerPhone ?? user.PhoneNumber,
            vendor.IdNumber,
            vendor.Nationality,
            vendor.PayoutCycle,
            vendor.FinancialLifecycleMode.ToString(),
            vendor.CommissionRate,
            NormalizeVendorStatus(vendor.Status),
            user.AccountStatus.ToString(),
            user.IsLoginLocked,
            user.LockedAtUtc,
            user.ArchivedAtUtc,
            vendor.SuspendedAtUtc,
            vendor.RejectionReason,
            vendor.SuspensionReason,
            vendor.LockReason,
            vendor.ArchiveReason,
            vendor.LogoUrl,
            vendor.CommercialRegisterDocumentUrl,
            vendor.TaxDocumentUrl,
            vendor.LicenseDocumentUrl,
            vendor.ApprovedAtUtc,
            approvedByName,
            vendor.CreatedAtUtc,
            vendor.UpdatedAtUtc,
            new VendorOperationsSettingsDto(
                vendor.AcceptOrders,
                vendor.MinimumOrderAmount,
                vendor.PreparationTimeMinutes),
            new VendorNotificationSettingsDto(
                vendor.EmailNotificationsEnabled,
                vendor.SmsNotificationsEnabled,
                vendor.NewOrdersNotificationsEnabled),
            vendor.Branches.Count,
            vendor.BankAccounts.Count,
            MapBankAccount(primaryBankAccount),
            MapOperatingHours(primaryBranch),
            workspaceReview.ReviewState,
            workspaceReview.CommercialAccessEnabled,
            null,
            workspaceReview.AssignedReviewerName,
            workspaceReview.ReviewSubmittedAtUtc,
            workspaceReview.ReviewStartedAtUtc,
            workspaceReview.ReviewCompletedAtUtc,
            workspaceReview.RequestedChangesAtUtc,
            workspaceReview.LastReviewDecision,
            workspaceReview.Summary,
            workspaceReview.Items,
            workspaceReview.RequiredActions,
            workspaceReview.AuditEntries,
            workspaceReview.MissingDocumentsCount,
            workspaceReview.CanSubmitForReview);
    }

    private static WorkspaceReviewProjection BuildWorkspaceReview(
        Vendor vendor,
        IReadOnlyList<VendorReviewDocumentDto> reviewDocuments,
        IReadOnlyList<Notification> reviewNotifications)
    {
        var requiredDocuments = reviewDocuments.Where(item => item.IsRequired).ToList();
        var missingRequired = requiredDocuments.Where(item => !item.IsUploaded).ToList();
        var rejectedRequired = requiredDocuments
            .Where(item => string.Equals(item.ReviewDecision, "rejected", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var approvedCount = reviewDocuments.Count(item =>
            string.Equals(item.ReviewDecision, "approved", StringComparison.OrdinalIgnoreCase));
        var changesRequestedCount = reviewDocuments.Count(item =>
            string.Equals(item.ReviewDecision, "rejected", StringComparison.OrdinalIgnoreCase));
        var submittedCount = reviewDocuments.Count(item =>
            item.IsUploaded
            && string.Equals(item.ReviewDecision, "pending", StringComparison.OrdinalIgnoreCase));
        var pendingVendorCount = reviewDocuments.Count(item => !item.IsUploaded);

        var reviewStartedAtUtc = reviewNotifications
            .FirstOrDefault(item => GetReviewKind(item.Type) == "start-review")
            ?.CreatedAtUtc;
        var reviewSubmittedAtUtc = reviewNotifications
            .FirstOrDefault(item => GetReviewKind(item.Type) == "submitted")
            ?.CreatedAtUtc;
        var requestedChangesAtUtc = reviewNotifications
            .FirstOrDefault(item => GetReviewKind(item.Type) == "request-documents")
            ?.CreatedAtUtc
            ?? vendor.DocumentReviews
                .Where(item => item.Decision == VendorDocumentReviewDecision.Rejected)
                .OrderByDescending(item => item.ReviewedAtUtc)
                .Select(item => item.ReviewedAtUtc)
                .FirstOrDefault();
        var reviewCompletedAtUtc = vendor.ApprovedAtUtc
            ?? reviewNotifications
                .FirstOrDefault(item => GetReviewKind(item.Type) is "approved" or "rejected")
                ?.CreatedAtUtc;
        var assignedReviewerName = reviewNotifications
            .FirstOrDefault(item => GetReviewKind(item.Type) is "start-review" or "document-approved" or "document-rejected")
            ?.Title;

        var requiredActions = missingRequired
            .Select(item => new VendorWorkspaceRequiredActionDto(
                item.Type,
                $"Please upload the required {NormalizeDocumentLabel(item.Type)} document."))
            .Concat(rejectedRequired.Select(item => new VendorWorkspaceRequiredActionDto(
                item.Type,
                string.IsNullOrWhiteSpace(item.RejectionReason)
                    ? $"Please re-upload the {NormalizeDocumentLabel(item.Type)} document."
                    : item.RejectionReason!)))
            .ToList();

        var canSubmitForReview = vendor.Status == VendorStatus.PendingReview
            && !vendor.ArchivedAtUtc.HasValue
            && missingRequired.Count == 0
            && rejectedRequired.Count == 0
            && !VendorReviewWorkflow.IsReadyForFinalApproval(vendor);

        var reviewState = ResolveWorkspaceReviewState(
            vendor,
            missingRequired.Count,
            rejectedRequired.Count,
            reviewStartedAtUtc,
            reviewSubmittedAtUtc);

        return new WorkspaceReviewProjection(
            reviewState,
            vendor.Status == VendorStatus.Active && vendor.ApprovedAtUtc.HasValue,
            assignedReviewerName,
            reviewSubmittedAtUtc,
            reviewStartedAtUtc,
            reviewCompletedAtUtc,
            requestedChangesAtUtc,
            ResolveLastReviewDecision(vendor, rejectedRequired.Count),
            new VendorWorkspaceReviewSummaryDto(
                reviewDocuments.Count,
                approvedCount,
                pendingVendorCount,
                submittedCount,
                changesRequestedCount,
                0),
            reviewDocuments.Select(MapWorkspaceReviewItem).ToList(),
            requiredActions,
            reviewNotifications.Select(MapWorkspaceAuditEntry).ToList(),
            missingRequired.Count + rejectedRequired.Count,
            canSubmitForReview);
    }

    private static VendorWorkspaceReviewItemDto MapWorkspaceReviewItem(VendorReviewDocumentDto document)
    {
        var status = document.ReviewDecision.ToLowerInvariant() switch
        {
            "approved" => "approved",
            "rejected" => "changes_requested",
            _ => document.IsUploaded ? "submitted" : "pending_vendor"
        };

        return new VendorWorkspaceReviewItemDto(
            document.Type,
            status,
            null,
            document.ReviewedByName,
            document.RejectionReason,
            document.IsUploaded ? document.ReviewedAtUtc : null,
            document.ReviewedAtUtc);
    }

    private static VendorWorkspaceReviewAuditEntryDto MapWorkspaceAuditEntry(Notification notification)
    {
        var meta = GetReviewMeta(notification.Type);

        return new VendorWorkspaceReviewAuditEntryDto(
            notification.Id.ToString(),
            meta.Kind,
            meta.Tone == "danger" ? "danger" : meta.Tone,
            notification.Body,
            meta.RoleLabel,
            notification.Title,
            notification.CreatedAtUtc,
            null,
            null);
    }

    private static string ResolveWorkspaceReviewState(
        Vendor vendor,
        int missingRequiredCount,
        int rejectedRequiredCount,
        DateTime? reviewStartedAtUtc,
        DateTime? reviewSubmittedAtUtc)
    {
        if (vendor.Status == VendorStatus.Active)
        {
            return "Verified";
        }

        if (vendor.Status == VendorStatus.Suspended)
        {
            return "Suspended";
        }

        if (vendor.Status == VendorStatus.Rejected)
        {
            return "Rejected";
        }

        if (rejectedRequiredCount > 0 || !string.IsNullOrWhiteSpace(vendor.RejectionReason))
        {
            return "ChangesRequested";
        }

        if (reviewStartedAtUtc.HasValue)
        {
            return "UnderReview";
        }

        if (reviewSubmittedAtUtc.HasValue || missingRequiredCount == 0)
        {
            return "Submitted";
        }

        return "AwaitingSubmission";
    }

    private static string? ResolveLastReviewDecision(Vendor vendor, int rejectedRequiredCount)
    {
        if (vendor.Status == VendorStatus.Active)
        {
            return "approved";
        }

        if (vendor.Status == VendorStatus.Rejected)
        {
            return "rejected";
        }

        if (rejectedRequiredCount > 0)
        {
            return "changes_requested";
        }

        return null;
    }

    private static string NormalizeDocumentLabel(string type) =>
        type.ToLowerInvariant() switch
        {
            "commercial" => "commercial registration",
            "tax" => "tax certificate",
            "license" => "municipal license",
            "bank" => "banking",
            "identity" => "owner identity",
            _ => "vendor"
        };

    private sealed record WorkspaceReviewProjection(
        string ReviewState,
        bool CommercialAccessEnabled,
        string? AssignedReviewerName,
        DateTime? ReviewSubmittedAtUtc,
        DateTime? ReviewStartedAtUtc,
        DateTime? ReviewCompletedAtUtc,
        DateTime? RequestedChangesAtUtc,
        string? LastReviewDecision,
        VendorWorkspaceReviewSummaryDto Summary,
        IReadOnlyList<VendorWorkspaceReviewItemDto> Items,
        IReadOnlyList<VendorWorkspaceRequiredActionDto> RequiredActions,
        IReadOnlyList<VendorWorkspaceReviewAuditEntryDto> AuditEntries,
        int MissingDocumentsCount,
        bool CanSubmitForReview);

    private static VendorDetailDto MapDetail(
        Vendor vendor,
        User user,
        string? approvedByName,
        IReadOnlyList<Notification> reviewNotifications)
    {
        var workspace = MapWorkspace(vendor, user, approvedByName, reviewNotifications);
        var reviewNotes = reviewNotifications
            .Select(MapReviewNote)
            .ToList();
        var reviewDocuments = MapReviewDocuments(vendor, workspace.PrimaryBankAccount, user);
        var latestRejectedDocumentReview = VendorReviewWorkflow.GetLatestRejectedRequiredReview(vendor);
        var reviewStartedAtUtc = reviewNotifications
            .FirstOrDefault(item => GetReviewKind(item.Type) == "start-review")
            ?.CreatedAtUtc;
        var requestedChangesAtUtc = reviewNotifications
            .FirstOrDefault(item => GetReviewKind(item.Type) == "request-documents")
            ?.CreatedAtUtc
            ?? latestRejectedDocumentReview?.ReviewedAtUtc;
        var reviewCompletedAtUtc = vendor.ApprovedAtUtc
            ?? reviewNotifications
                .FirstOrDefault(item =>
                    GetReviewKind(item.Type) == "approved"
                    || GetReviewKind(item.Type) == "rejected")
                ?.CreatedAtUtc;
        var reviewDecisionReason = latestRejectedDocumentReview?.RejectionReason
            ?? reviewNotifications
            .FirstOrDefault(item =>
                GetReviewKind(item.Type) == "request-documents"
                || GetReviewKind(item.Type) == "rejected"
                || GetReviewKind(item.Type) == "suspended"
                || GetReviewKind(item.Type) == "archived"
                || GetReviewKind(item.Type) == "locked")
            ?.Body
            ?? workspace.RejectionReason
            ?? workspace.SuspensionReason
            ?? workspace.ArchiveReason
            ?? workspace.LockReason;

        return new VendorDetailDto(
            workspace.Id,
            workspace.BusinessNameAr,
            workspace.BusinessNameEn,
            workspace.BusinessType,
            workspace.CommercialRegistrationNumber,
            workspace.CommercialRegistrationExpiryDate,
            workspace.TaxId,
            workspace.LicenseNumber,
            workspace.ContactEmail,
            workspace.ContactPhone,
            workspace.DescriptionAr,
            workspace.DescriptionEn,
            workspace.Region,
            workspace.City,
            workspace.NationalAddress,
            workspace.CommissionRate,
            workspace.Status,
            workspace.AccountStatus,
            workspace.IsLoginLocked,
            workspace.LockedAtUtc,
            workspace.ArchivedAtUtc,
            workspace.SuspendedAtUtc,
            workspace.RejectionReason,
            workspace.SuspensionReason,
            workspace.LockReason,
            workspace.ArchiveReason,
            workspace.LogoUrl,
            workspace.CommercialRegisterDocumentUrl,
            workspace.TaxDocumentUrl,
            workspace.LicenseDocumentUrl,
            workspace.ApprovedAtUtc,
            workspace.ApprovedByName,
            workspace.CreatedAtUtc,
            workspace.UpdatedAtUtc,
            reviewStartedAtUtc,
            reviewCompletedAtUtc,
            requestedChangesAtUtc,
            reviewDecisionReason,
            VendorReviewWorkflow.IsReadyForFinalApproval(vendor),
            workspace.OwnerName ?? user.FullName,
            workspace.OwnerEmail ?? user.Email ?? string.Empty,
            workspace.OwnerPhone ?? user.PhoneNumber ?? string.Empty,
            workspace.IdNumber,
            workspace.Nationality,
            workspace.PayoutCycle,
            workspace.FinancialLifecycleMode,
            workspace.OperationsSettings,
            workspace.NotificationSettings,
            workspace.PrimaryBankAccount,
            workspace.OperatingHours,
            reviewDocuments,
            reviewNotes,
            workspace.BranchesCount,
            workspace.BankAccountsCount);
    }

    private static IReadOnlyList<VendorReviewDocumentDto> MapReviewDocuments(
        Vendor vendor,
        VendorBankAccountDto? primaryBankAccount,
        User user)
    {
        var reviewLookup = vendor.DocumentReviews.ToDictionary(item => item.Type);

        return new[]
        {
            CreateReviewDocument(vendor, user, primaryBankAccount, reviewLookup, VendorDocumentType.Identity),
            CreateReviewDocument(vendor, user, primaryBankAccount, reviewLookup, VendorDocumentType.Commercial),
            CreateReviewDocument(vendor, user, primaryBankAccount, reviewLookup, VendorDocumentType.Tax),
            CreateReviewDocument(vendor, user, primaryBankAccount, reviewLookup, VendorDocumentType.Bank),
            CreateReviewDocument(vendor, user, primaryBankAccount, reviewLookup, VendorDocumentType.License)
        };
    }

    private static VendorReviewDocumentDto CreateReviewDocument(
        Vendor vendor,
        User user,
        VendorBankAccountDto? primaryBankAccount,
        IReadOnlyDictionary<VendorDocumentType, VendorDocumentReview> reviewLookup,
        VendorDocumentType type)
    {
        _ = user;
        _ = primaryBankAccount;

        var review = reviewLookup.GetValueOrDefault(type);
        var (titleKey, descriptionKey, icon) = GetReviewDocumentMetadata(type);
        var fileUrl = GetDocumentFileUrl(vendor, type);
        var isUploaded = VendorReviewWorkflow.IsUploaded(vendor, type);
        var status = VendorReviewWorkflow.ResolveStatus(isUploaded, review?.Decision);
        var previewKind = VendorReviewWorkflow.ResolvePreviewKind(fileUrl, isUploaded);
        var reviewDecision = review?.Decision.ToString().ToLowerInvariant() ?? "pending";

        return new VendorReviewDocumentDto(
            type.ToString().ToLowerInvariant(),
            type.ToString().ToLowerInvariant(),
            titleKey,
            descriptionKey,
            icon,
            status,
            VendorReviewWorkflow.ResolveStatusLabelKey(status),
            VendorReviewWorkflow.ResolveStatusBadgeClass(status),
            VendorReviewWorkflow.IsRequired(type),
            isUploaded,
            previewKind,
            fileUrl,
            reviewDecision,
            review?.RejectionReason,
            review?.ReviewedAtUtc,
            review?.ReviewedByName);
    }

    private static (string TitleKey, string DescriptionKey, string Icon) GetReviewDocumentMetadata(VendorDocumentType type) =>
        type switch
        {
            VendorDocumentType.Identity => ("COMPLIANCE.VERIFICATION.IDENTITY", "COMPLIANCE.VERIFICATION.IDENTITY_DESC", "badge"),
            VendorDocumentType.Commercial => ("COMPLIANCE.VERIFICATION.COMMERCIAL_REG", "COMPLIANCE.VERIFICATION.COMMERCIAL_DESC", "storefront"),
            VendorDocumentType.Tax => ("COMPLIANCE.VERIFICATION.TAX_CERT", "COMPLIANCE.VERIFICATION.TAX_DESC", "receipt_long"),
            VendorDocumentType.Bank => ("COMPLIANCE.VERIFICATION.BANK_ACCOUNT", "COMPLIANCE.VERIFICATION.BANK_DESC", "account_balance"),
            _ => ("COMPLIANCE.VERIFICATION.MUNICIPAL_LICENSE", "COMPLIANCE.VERIFICATION.LICENSE_DESC", "verified")
        };

    private static string? GetDocumentFileUrl(Vendor vendor, VendorDocumentType type) =>
        type switch
        {
            VendorDocumentType.Commercial => vendor.CommercialRegisterDocumentUrl,
            VendorDocumentType.Tax => vendor.TaxDocumentUrl,
            VendorDocumentType.License => vendor.LicenseDocumentUrl,
            _ => null
        };

    private static VendorReviewNoteDto MapReviewNote(Notification notification)
    {
        var tone = GetReviewTone(notification.Type);
        var roleLabel = GetReviewRole(notification.Type);
        var kind = GetReviewKind(notification.Type);

        return new VendorReviewNoteDto(
            notification.Id.ToString(),
            notification.Title,
            roleLabel,
            notification.CreatedAtUtc,
            notification.Body,
            null,
            tone,
            kind != "note");
    }

    private static VendorActivityLogEntryDto MapActivityLogEntry(Notification notification)
    {
        var meta = GetActivityMeta(notification.Type);

        return new VendorActivityLogEntryDto(
            notification.Id.ToString(),
            meta.Kind,
            meta.Severity,
            notification.Title,
            meta.RoleLabel,
            notification.CreatedAtUtc,
            notification.Body,
            meta.Kind != "note");
    }

    private static VendorBranch? GetPrimaryBranch(Vendor vendor) =>
        vendor.Branches
            .OrderByDescending(branch => branch.IsActive)
            .ThenBy(branch => branch.CreatedAtUtc)
            .FirstOrDefault();

    private static VendorBankAccount? GetPrimaryBankAccount(Vendor vendor) =>
        vendor.BankAccounts
            .OrderByDescending(account => account.IsPrimary)
            .ThenByDescending(account => account.VerifiedAtUtc)
            .ThenBy(account => account.CreatedAtUtc)
            .FirstOrDefault();

    private static VendorBankAccountDto? MapBankAccount(VendorBankAccount? account) =>
        account == null
            ? null
            : new VendorBankAccountDto(
                account.Id,
                account.BankName,
                account.AccountHolderName,
                account.IBAN,
                account.SwiftCode,
                account.IsPrimary,
                account.Status.ToString(),
                account.RejectionReason,
                account.VerifiedAtUtc);

    private static IReadOnlyList<VendorOperatingHourDto> MapOperatingHours(VendorBranch? branch) =>
        branch?.OperatingHours
            .OrderBy(item => item.DayOfWeek)
            .Select(item => new VendorOperatingHourDto(
                item.DayOfWeek,
                item.OpenTime.ToString(@"hh\:mm"),
                item.CloseTime.ToString(@"hh\:mm"),
                !item.IsClosed))
            .ToList()
        ?? [];

    private static string NormalizeVendorStatus(VendorStatus status) =>
        status == VendorStatus.PendingReview ? "Pending" : status.ToString();

    private static string GetReviewKind(string? type) => GetReviewMeta(type).Kind;

    private static string GetReviewTone(string? type) => GetReviewMeta(type).Tone;

    private static string GetReviewRole(string? type) => GetReviewMeta(type).RoleLabel;

    private static string? NormalizeActivityFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "all" or "*" ? null : normalized;
    }

    private static (string Kind, string Tone, string RoleLabel) GetReviewMeta(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return ("note", "info", "Vendor Review");
        }

        var parts = type.Split('|', 4, StringSplitOptions.TrimEntries);
        if (parts.Length < 4 || !string.Equals(parts[0], "vendor-review", StringComparison.OrdinalIgnoreCase))
        {
            return ("note", "info", "Vendor Review");
        }

        return (parts[1], parts[2], parts[3]);
    }

    private static (string Kind, string Severity, string RoleLabel) GetActivityMeta(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return ("note", "info", "Vendor Activity");
        }

        var parts = type.Split('|', 4, StringSplitOptions.TrimEntries);
        if (parts.Length < 4 || !string.Equals(parts[0], "vendor-activity", StringComparison.OrdinalIgnoreCase))
        {
            return ("note", "info", "Vendor Activity");
        }

        return (parts[1], parts[2], parts[3]);
    }
}
