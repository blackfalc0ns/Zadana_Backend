using FluentAssertions;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.UnitTests.Modules.Vendors.Entities;

public class VendorPrimaryBranchNameSyncTests
{
    [Fact]
    public void UpdateStore_RenamesPrimaryBranchToStoreArabicName()
    {
        var vendor = CreateVendor();
        var primary = CreateBranch(vendor.Id, "اسواق النخبة", isPrimary: true);
        var secondary = CreateBranch(vendor.Id, "فرع الخبر", isPrimary: false);
        vendor.Branches.Add(primary);
        vendor.Branches.Add(secondary);

        vendor.UpdateStore(
            "a",
            "Store A",
            "Retail",
            "vendor@test.com",
            "123",
            null,
            null,
            null,
            null);

        primary.Name.Should().Be("a");
        secondary.Name.Should().Be("فرع الخبر");
    }

    [Fact]
    public void UpdateProfile_RenamesPrimaryBranchToStoreArabicName()
    {
        var vendor = CreateVendor();
        var primary = CreateBranch(vendor.Id, "اسواق النخبة", isPrimary: true);
        vendor.Branches.Add(primary);

        vendor.UpdateProfile("a", "Store A", "Retail", "vendor@test.com", "123", null);

        primary.Name.Should().Be("a");
    }

    [Fact]
    public void UpdateStore_DoesNothingWhenVendorHasNoPrimaryBranch()
    {
        var vendor = CreateVendor();
        var secondary = CreateBranch(vendor.Id, "فرع الخبر", isPrimary: false);
        vendor.Branches.Add(secondary);

        var act = () => vendor.UpdateStore(
            "a",
            "Store A",
            "Retail",
            "vendor@test.com",
            "123",
            null,
            null,
            null,
            null);

        act.Should().NotThrow();
        secondary.Name.Should().Be("فرع الخبر");
    }

    private static Vendor CreateVendor() =>
        new(Guid.NewGuid(), "متجر B", "Store B", "Retail", "CR-1", "vendor@test.com", "123");

    private static VendorBranch CreateBranch(Guid vendorId, string name, bool isPrimary) =>
        new(
            vendorId,
            name,
            isPrimary ? "MAIN" : "SEC",
            isPrimary,
            "Dammam",
            "EASTERN",
            "DAMMAM",
            26.3927m,
            49.9777m,
            "0500000000",
            "Manager",
            "0500000000",
            5m);
}
