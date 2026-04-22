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

    public async Task<VendorWorkspaceDto?> GetWorkspaceByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var vendor = await _dbContext.Vendors
            .AsNoTracking()
            .Include(item => item.Branches)
                .ThenInclude(branch => branch.OperatingHours)
            .Include(item => item.BankAccounts)
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

        return MapWorkspace(vendor, user, approvedByName);
    }

    public Task<Guid?> GetVendorIdByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Vendors
            .AsNoTracking()
            .Where(vendor => vendor.UserId == userId)
            .Select(vendor => (Guid?)vendor.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static VendorWorkspaceDto MapWorkspace(Vendor vendor, User user, string? approvedByName)
    {
        var primaryBankAccount = GetPrimaryBankAccount(vendor);
        var primaryBranch = GetPrimaryBranch(vendor);

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
            MapOperatingHours(primaryBranch));
    }

    private static VendorDetailDto MapDetail(
        Vendor vendor,
        User user,
        string? approvedByName,
        IReadOnlyList<Notification> reviewNotifications)
    {
        var workspace = MapWorkspace(vendor, user, approvedByName);
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
}
