using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Api.Modules.Files.Security;

public sealed record FileUploadSecurityRule(
    string Directory,
    bool AllowAnonymous,
    long MaxFileSizeBytes,
    IReadOnlySet<string> AllowedRoles,
    bool RequiresRegistrationToken);

public static class FileUploadSecurityPolicy
{
    private const long DefaultMaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, FileUploadSecurityRule> Rules =
        new Dictionary<string, FileUploadSecurityRule>(StringComparer.OrdinalIgnoreCase)
        {
            // Public-ish branding; no token required.
            [NormalizeDirectory("uploads/vendors/logos")] =
                CreatePublicAnonymous("uploads/vendors/logos"),

            // PII-sensitive vendor docs: anonymous allowed (during signup),
            // but caller MUST present a registration upload token.
            [NormalizeDirectory("uploads/vendors/commercial-register")] =
                CreateRegistrationOnly("uploads/vendors/commercial-register"),
            [NormalizeDirectory("uploads/vendors/tax-certificates")] =
                CreateRegistrationOnly("uploads/vendors/tax-certificates"),
            [NormalizeDirectory("uploads/vendors/licenses")] =
                CreateRegistrationOnly("uploads/vendors/licenses"),

            [NormalizeDirectory("uploads/users/profile")] =
                CreateAuthenticated("uploads/users/profile"),

            [NormalizeDirectory("uploads/orders/disputes/evidence")] =
                CreateAuthenticatedRoles("uploads/orders/disputes/evidence", UserRole.Admin, UserRole.SuperAdmin),

            // Driver registration happens before the driver has an account.
            // Keep these anonymous so the mobile app can upload the selected
            // files and submit their returned URLs with the register request.
            [NormalizeDirectory("drivers/national-id")] =
                CreatePublicAnonymous("drivers/national-id"),
            [NormalizeDirectory("drivers/license")] =
                CreatePublicAnonymous("drivers/license"),
            [NormalizeDirectory("drivers/vehicle")] =
                CreatePublicAnonymous("drivers/vehicle"),
            [NormalizeDirectory("drivers/profile")] =
                CreatePublicAnonymous("drivers/profile"),

            [NormalizeDirectory("drivers/proofs")] =
                CreateAuthenticatedRoles("drivers/proofs", UserRole.Driver),

            [NormalizeDirectory("uploads/catalog/brand-requests")] =
                CreateAuthenticatedRoles("uploads/catalog/brand-requests", UserRole.Vendor, UserRole.VendorStaff),
            [NormalizeDirectory("uploads/catalog/category-requests")] =
                CreateAuthenticatedRoles("uploads/catalog/category-requests", UserRole.Vendor, UserRole.VendorStaff),
            [NormalizeDirectory("uploads/catalog/product-requests")] =
                CreateAuthenticatedRoles("uploads/catalog/product-requests", UserRole.Vendor, UserRole.VendorStaff),

            [NormalizeDirectory("uploads/catalog/categories")] =
                CreatePublicAnonymous("uploads/catalog/categories"),
            [NormalizeDirectory("uploads/catalog/brands")] =
                CreatePublicAnonymous("uploads/catalog/brands"),
            [NormalizeDirectory("uploads/catalog/products")] =
                CreatePublicAnonymous("uploads/catalog/products"),

            // Aliases (kept for mobile backward compatibility).
            [NormalizeDirectory("categories")] =
                CreatePublicAnonymous("uploads/catalog/categories"),
            [NormalizeDirectory("brands")] =
                CreatePublicAnonymous("uploads/catalog/brands"),
            [NormalizeDirectory("products")] =
                CreatePublicAnonymous("uploads/catalog/products"),
            [NormalizeDirectory("catalog")] =
                CreatePublicAnonymous("uploads/catalog/products")
        };

    public static string NormalizeDirectory(string? directory)
    {
        var normalized = (directory ?? string.Empty)
            .Replace('\\', '/')
            .Trim();

        // Reject path-traversal attempts up front.
        if (normalized.Contains("..", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        return normalized.Trim('/');
    }

    public static bool TryResolve(string? directory, out FileUploadSecurityRule rule)
    {
        return Rules.TryGetValue(NormalizeDirectory(directory), out rule!);
    }

    private static FileUploadSecurityRule CreatePublicAnonymous(string directory) =>
        new(
            NormalizeDirectory(directory),
            AllowAnonymous: true,
            DefaultMaxFileSizeBytes,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            RequiresRegistrationToken: false);

    private static FileUploadSecurityRule CreateRegistrationOnly(string directory) =>
        new(
            NormalizeDirectory(directory),
            AllowAnonymous: true,
            DefaultMaxFileSizeBytes,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            RequiresRegistrationToken: true);

    private static FileUploadSecurityRule CreateAuthenticated(string directory) =>
        new(
            NormalizeDirectory(directory),
            AllowAnonymous: false,
            DefaultMaxFileSizeBytes,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            RequiresRegistrationToken: false);

    private static FileUploadSecurityRule CreateAuthenticatedRoles(string directory, params UserRole[] allowedRoles) =>
        new(
            NormalizeDirectory(directory),
            AllowAnonymous: false,
            DefaultMaxFileSizeBytes,
            allowedRoles
                .Select(role => role.ToString())
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            RequiresRegistrationToken: false);
}
