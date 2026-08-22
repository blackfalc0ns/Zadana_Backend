using System.Globalization;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Application.Modules.Vendors.Support;

public static class VendorDisplayNames
{
    public static string ResolvePickupBranchName(VendorBranch branch)
    {
        var storeName = PickLocalized(branch.Vendor?.BusinessNameAr, branch.Vendor?.BusinessNameEn);
        var branchName = branch.Name?.Trim() ?? string.Empty;
        var suffix = StripKnownStorePrefix(branchName, branch.Vendor?.BusinessNameAr, branch.Vendor?.BusinessNameEn);

        if (string.IsNullOrWhiteSpace(storeName))
        {
            return string.IsNullOrWhiteSpace(suffix) ? branchName : suffix;
        }

        return string.IsNullOrWhiteSpace(suffix) ? storeName : $"{storeName} — {suffix}";
    }

    private static string PickLocalized(string? arabic, string? english)
    {
        var preferred = IsArabic() ? arabic : english;
        var fallback = IsArabic() ? english : arabic;
        return preferred?.Trim() ?? fallback?.Trim() ?? string.Empty;
    }

    private static bool IsArabic() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    private static string StripKnownStorePrefix(string branchName, params string?[] storeNames)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return string.Empty;
        }

        foreach (var store in storeNames
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .OrderByDescending(value => value.Length))
        {
            if (branchName.Equals(store, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (branchName.StartsWith(store, StringComparison.OrdinalIgnoreCase))
            {
                return branchName[store.Length..].Trim().TrimStart('-', '–', '—', ' ').Trim();
            }
        }

        return branchName;
    }
}
