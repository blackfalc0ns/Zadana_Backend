using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Application.Modules.Vendors.Support;

public static class VendorProfileReviewMutations
{
    public static void ResetSectionToSubmitted(Vendor vendor, string section)
    {
        foreach (var code in VendorProfileReviewCatalog.GetSectionCodes(section))
        {
            ResetCodeToSubmitted(vendor, code);
        }
    }

    public static void ResetCodeToSubmitted(Vendor vendor, string code)
    {
        if (!VendorProfileReviewCatalog.TryGetDefinition(code, out var definition))
        {
            return;
        }

        var review = vendor.ProfileReviewItems.FirstOrDefault(item =>
            string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));

        if (review == null)
        {
            vendor.ProfileReviewItems.Add(new VendorProfileReviewItem(vendor.Id, code, definition.TargetType, definition.Step));
            return;
        }

        review.MarkSubmitted();
    }

    public static VendorProfileReviewItem GetOrCreate(Vendor vendor, string code)
    {
        if (!VendorProfileReviewCatalog.TryGetDefinition(code, out var definition))
        {
            throw new InvalidOperationException($"Unknown vendor review code '{code}'.");
        }

        var review = vendor.ProfileReviewItems.FirstOrDefault(item =>
            string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));

        if (review != null)
        {
            return review;
        }

        review = new VendorProfileReviewItem(vendor.Id, code, definition.TargetType, definition.Step);
        vendor.ProfileReviewItems.Add(review);
        return review;
    }
}
