using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Application.Modules.Vendors.Support;

public static class VendorPrimaryBranchFactory
{
    private const int BranchNameMaxLength = 200;
    private const int BranchCodeMaxLength = 50;
    private const int AddressMaxLength = 500;
    private const int RegionMaxLength = 100;
    private const int CityMaxLength = 100;
    private const int ContactPhoneMaxLength = 20;

    public static VendorBranch CreateForHoursProfile(Vendor vendor)
    {
        var branchName = FirstNonBlank(vendor.BusinessNameAr, vendor.BusinessNameEn, "Primary branch");

        return new VendorBranch(
            vendor.Id,
            Truncate(branchName, BranchNameMaxLength),
            Truncate(branchName, BranchCodeMaxLength),
            true,
            Truncate(FirstNonBlank(vendor.NationalAddress, "Primary branch"), AddressMaxLength),
            Truncate(vendor.Region ?? string.Empty, RegionMaxLength),
            Truncate(vendor.City ?? string.Empty, CityMaxLength),
            0,
            0,
            Truncate(FirstNonBlank(vendor.ContactPhone, vendor.OwnerPhone, string.Empty), ContactPhoneMaxLength),
            string.Empty,
            string.Empty,
            5);
    }

    private static string FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
