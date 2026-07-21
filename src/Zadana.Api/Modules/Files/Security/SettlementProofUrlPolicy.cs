using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Settings;

namespace Zadana.Api.Modules.Files.Security;

/// <summary>
/// Accepts only URLs that the configured file-storage provider produced for
/// the finance-proof folder.  A generic HTTP(S) URL is not evidence: without
/// this boundary an administrator could attach a remote, mutable document or
/// a file from an unrelated upload folder to a payout.
/// </summary>
public sealed class SettlementProofUrlPolicy : ISettlementProofReferenceValidator
{
    private static readonly string[] ProofDirectorySegments = ["uploads", "settlements", "proofs"];

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".pdf"
        };

    private readonly FileStorageSettings _fileStorageSettings;
    private readonly ImageKitSettings _imageKitSettings;

    public SettlementProofUrlPolicy(
        IOptions<FileStorageSettings> fileStorageOptions,
        IOptions<ImageKitSettings> imageKitOptions)
    {
        _fileStorageSettings = fileStorageOptions.Value;
        _imageKitSettings = imageKitOptions.Value;
    }

    public bool IsValid(string? proofUrl)
    {
        if (!Uri.TryCreate(proofUrl?.Trim(), UriKind.Absolute, out var proofUri) ||
            (proofUri.Scheme != Uri.UriSchemeHttps && proofUri.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(proofUri.UserInfo) ||
            !string.IsNullOrEmpty(proofUri.Fragment))
        {
            return false;
        }

        var configuredBaseUrl = _fileStorageSettings.Provider.Equals("Local", StringComparison.OrdinalIgnoreCase)
            ? _fileStorageSettings.Local.PublicBaseUrl
            : _imageKitSettings.UrlEndpoint;

        if (!Uri.TryCreate(configuredBaseUrl?.TrimEnd('/') + "/", UriKind.Absolute, out var storageBaseUri) ||
            !HasSameOrigin(storageBaseUri, proofUri))
        {
            return false;
        }

        return IsSettlementProofPath(storageBaseUri, proofUri);
    }

    private static bool IsSettlementProofPath(Uri storageBaseUri, Uri proofUri)
    {
        var baseSegments = GetSafePathSegments(storageBaseUri.AbsolutePath);
        var proofSegments = GetSafePathSegments(proofUri.AbsolutePath);
        if (baseSegments is null || proofSegments is null || proofSegments.Count <= baseSegments.Count)
        {
            return false;
        }

        if (!proofSegments.Take(baseSegments.Count)
                .SequenceEqual(baseSegments, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativeSegments = proofSegments.Skip(baseSegments.Count).ToArray();
        if (relativeSegments.Length != ProofDirectorySegments.Length + 1 ||
            !relativeSegments.Take(ProofDirectorySegments.Length)
                .SequenceEqual(ProofDirectorySegments, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = relativeSegments[^1];
        return !string.IsNullOrWhiteSpace(fileName) &&
               fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               AllowedExtensions.Contains(Path.GetExtension(fileName));
    }

    private static List<string>? GetSafePathSegments(string absolutePath)
    {
        string unescapedPath;
        try
        {
            unescapedPath = Uri.UnescapeDataString(absolutePath);
        }
        catch (UriFormatException)
        {
            return null;
        }

        var segments = unescapedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        return segments.Any(segment =>
                segment is "." or ".." ||
                segment.Contains('\\') ||
                segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            ? null
            : segments;
    }

    private static bool HasSameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
        left.Port == right.Port;
}
