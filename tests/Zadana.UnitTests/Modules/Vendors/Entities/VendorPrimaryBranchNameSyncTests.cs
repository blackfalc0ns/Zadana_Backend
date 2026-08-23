using FluentAssertions;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

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
    public void SetStoreLocation_UpdatesPrimaryBranchCoordinatesOnly()
    {
        var vendor = CreateVendor();
        var primary = CreateBranch(vendor.Id, "اسواق النخبة", isPrimary: true, 24.7136m, 46.6753m);
        var secondary = CreateBranch(vendor.Id, "فرع الخبر", isPrimary: false, 26.2173m, 50.1971m);
        vendor.Branches.Add(primary);
        vendor.Branches.Add(secondary);

        vendor.SetStoreLocation(26.3927m, 49.9777m);

        primary.Latitude.Should().Be(26.3927m);
        primary.Longitude.Should().Be(49.9777m);
        secondary.Latitude.Should().Be(26.2173m);
        secondary.Longitude.Should().Be(50.1971m);
        secondary.Name.Should().Be("فرع الخبر");
    }

    [Fact]
    public void SetStoreLocation_PromotesFirstBranchWhenNoPrimaryExists()
    {
        var vendor = CreateVendor();
        var secondary = CreateBranch(vendor.Id, "فرع الخبر", isPrimary: false, 26.2173m, 50.1971m);
        vendor.Branches.Add(secondary);

        vendor.SetStoreLocation(26.3927m, 49.9777m);

        secondary.IsPrimary.Should().BeTrue();
        secondary.Latitude.Should().Be(26.3927m);
        secondary.Longitude.Should().Be(49.9777m);
    }

    [Fact]
    public void SetStoreLocation_ThrowsWhenVendorHasNoBranches()
    {
        var vendor = CreateVendor();

        var act = () => vendor.SetStoreLocation(26.3927m, 49.9777m);

        act.Should()
            .Throw<BusinessRuleException>()
            .Which.ErrorCode.Should().Be("PRIMARY_BRANCH_REQUIRED");
    }

    [Fact]
    public void UpdateContact_SyncsPrimaryBranchAddressFields()
    {
        var vendor = CreateVendor();
        var primary = CreateBranch(vendor.Id, "اسواق النخبة", isPrimary: true);
        vendor.Branches.Add(primary);

        vendor.UpdateContact("EASTERN", "DAMMAM", "National Address 1");

        primary.Region.Should().Be("EASTERN");
        primary.City.Should().Be("DAMMAM");
        primary.AddressLine.Should().Be("National Address 1");
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

    private static VendorBranch CreateBranch(
        Guid vendorId,
        string name,
        bool isPrimary,
        decimal latitude = 26.3927m,
        decimal longitude = 49.9777m) =>
        new(
            vendorId,
            name,
            isPrimary ? "MAIN" : "SEC",
            isPrimary,
            "Dammam",
            "EASTERN",
            "DAMMAM",
            latitude,
            longitude,
            "0500000000",
            "Manager",
            "0500000000",
            5m);
}
