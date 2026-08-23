using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Support;

public static class VendorPrimaryBranchFactory
{
    private const int BranchNameMaxLength = 200;
    private const int BranchCodeMaxLength = 50;
    private const int AddressMaxLength = 500;
    private const int RegionMaxLength = 100;
    private const int CityMaxLength = 100;
    private const int ContactPhoneMaxLength = 20;

    public static VendorBranch CreateForHoursProfile(
        Vendor vendor,
        decimal latitude,
        decimal longitude)
    {
        VendorBranchCoordinateValidation.EnsureValid(latitude, longitude);

        var branchName = FirstNonBlank(vendor.BusinessNameAr, vendor.BusinessNameEn, "Primary branch");

        return new VendorBranch(
            vendor.Id,
            Truncate(branchName, BranchNameMaxLength),
            Truncate(branchName, BranchCodeMaxLength),
            true,
            Truncate(FirstNonBlank(vendor.NationalAddress, "Primary branch"), AddressMaxLength),
            Truncate(vendor.Region ?? string.Empty, RegionMaxLength),
            Truncate(vendor.City ?? string.Empty, CityMaxLength),
            latitude,
            longitude,
            Truncate(FirstNonBlank(vendor.ContactPhone, vendor.OwnerPhone, string.Empty), ContactPhoneMaxLength),
            string.Empty,
            string.Empty,
            5);
    }

    public static VendorBranch RequireExistingOrThrow(Vendor vendor) =>
        vendor.Branches
            .OrderByDescending(branch => branch.IsPrimary)
            .ThenByDescending(branch => branch.IsActive)
            .ThenBy(branch => branch.CreatedAtUtc)
            .FirstOrDefault()
        ?? throw new BusinessRuleException(
            "PRIMARY_BRANCH_REQUIRED",
            "لازم تضيف فرع أساسي بإحداثيات صحيحة قبل تحديث ساعات العمل.|Add a primary branch with valid coordinates before updating hours.");

    private static string FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
