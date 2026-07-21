using FluentAssertions;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.UnitTests.Modules.Vendors.Entities;

public class VendorStatusTransitionTests
{
    [Fact]
    public void LockAndUnlock_FromRejectedVendor_KeepsRejectedStatus()
    {
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "vendor@test.com", "123");

        vendor.Reject("Missing tax certificate");
        vendor.Lock("Security review");
        vendor.Unlock();

        vendor.Status.Should().Be(VendorStatus.Rejected);
        vendor.LockReason.Should().BeNull();
        vendor.LockedAtUtc.Should().BeNull();
    }

    [Fact]
    public void LockAndUnlock_FromActiveVendor_RestoresActiveStatus()
    {
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "vendor@test.com", "123");
        vendor.Approve(12, Guid.NewGuid());

        vendor.Lock("Security review");
        vendor.Unlock();

        vendor.Status.Should().Be(VendorStatus.Active);
        vendor.LockReason.Should().BeNull();
        vendor.LockedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Reactivate_FromSuspendedVendor_RestoresActiveStatus()
    {
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "vendor@test.com", "123");
        var adminId = Guid.NewGuid();
        vendor.Approve(12, adminId);
        vendor.Suspend("Manual review");

        vendor.Reactivate(adminId);

        vendor.Status.Should().Be(VendorStatus.Active);
        vendor.SuspensionReason.Should().BeNull();
        vendor.SuspendedAtUtc.Should().BeNull();
        vendor.LockReason.Should().BeNull();
        vendor.LockedAtUtc.Should().BeNull();
    }

    [Fact]
    public void LegacyPerOrderFinanceMode_IsNormalizedToWeeklyMonday()
    {
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "vendor@test.com", "123");

        vendor.UpdateFinanceSettings(VendorFinancialLifecycleMode.PerOrderDirectPayout);

        vendor.FinancialLifecycleMode.Should().Be(VendorFinancialLifecycleMode.Weekly);
        vendor.PayoutCycle.Should().Be("weekly");
        vendor.PayoutDay.Should().Be(PayoutScheduleDay.Monday);
    }

    [Fact]
    public void UpdatePayoutDay_AllowsThursday()
    {
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "vendor@test.com", "123");

        vendor.UpdatePayoutDay(PayoutScheduleDay.Thursday);

        vendor.PayoutDay.Should().Be(PayoutScheduleDay.Thursday);
    }
}
