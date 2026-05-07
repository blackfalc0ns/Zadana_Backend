using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Api.Modules.Files.Security;

public sealed record FileUploadSecurityRule(
    string Directory,
    bool AllowAnonymous,
    long MaxFileSizeBytes,
    IReadOnlySet<string> AllowedRoles);

public static class FileUploadSecurityPolicy
{
    private const long DefaultMaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, FileUploadSecurityRule> Rules =
        new Dictionary<string, FileUploadSecurityRule>(StringComparer.OrdinalIgnoreCase)
        {
            [NormalizeDirectory("uploads/vendors/logos")] = Create("uploads/vendors/logos", allowAnonymous: true),
            [NormalizeDirectory("uploads/vendors/commercial-register")] = Create("uploads/vendors/commercial-register", allowAnonymous: true),
            [NormalizeDirectory("uploads/vendors/tax-certificates")] = Create("uploads/vendors/tax-certificates", allowAnonymous: true),
            [NormalizeDirectory("uploads/vendors/licenses")] = Create("uploads/vendors/licenses", allowAnonymous: true),
            [NormalizeDirectory("drivers/national-id")] = Create("drivers/national-id", allowAnonymous: true),
            [NormalizeDirectory("drivers/license")] = Create("drivers/license", allowAnonymous: true),
            [NormalizeDirectory("drivers/vehicle")] = Create("drivers/vehicle", allowAnonymous: true),
            [NormalizeDirectory("drivers/profile")] = Create("drivers/profile", allowAnonymous: true),
            [NormalizeDirectory("drivers/proofs")] = Create("drivers/proofs", allowAnonymous: false, UserRole.Driver),
            [NormalizeDirectory("uploads/catalog/brand-requests")] = Create("uploads/catalog/brand-requests", allowAnonymous: false, UserRole.Vendor, UserRole.VendorStaff),
            [NormalizeDirectory("uploads/catalog/category-requests")] = Create("uploads/catalog/category-requests", allowAnonymous: false, UserRole.Vendor, UserRole.VendorStaff)
        };

    public static string NormalizeDirectory(string? directory)
    {
        var normalized = (directory ?? string.Empty)
            .Replace('\\', '/')
            .Trim();

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

    private static FileUploadSecurityRule Create(string directory, bool allowAnonymous, params UserRole[] allowedRoles)
    {
        return new FileUploadSecurityRule(
            NormalizeDirectory(directory),
            allowAnonymous,
            DefaultMaxFileSizeBytes,
            allowedRoles
                .Select(role => role.ToString())
                .ToHashSet(StringComparer.OrdinalIgnoreCase));
    }
}
