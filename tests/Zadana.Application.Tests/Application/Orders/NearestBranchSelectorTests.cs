using FluentAssertions;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Orders.Support;

namespace Zadana.Application.Tests.Application.Orders;

public class NearestBranchSelectorTests
{
    [Fact]
    public void MaxMatchKm_ShouldBeFifty()
    {
        DeliveryProximityLimits.MaxMatchKm.Should().Be(50m);
    }

    [Fact]
    public void Order_ShouldPickCloserKhobarBranchOverFartherDammamBranch()
    {
        var dammam = new FakeBranch(Guid.NewGuid(), 26.43m, 50.08m, IsPrimary: true);
        var khobar = new FakeBranch(Guid.NewGuid(), 26.22m, 50.19m, IsPrimary: false);
        var ordered = NearestBranchSelector.Order(
            [dammam, khobar],
            customerLatitude: 26.2172m,
            customerLongitude: 50.1971m,
            latitude: b => b.Latitude,
            longitude: b => b.Longitude,
            isPrimary: b => b.IsPrimary,
            createdAtUtc: b => b.CreatedAtUtc).ToList();

        ordered.Should().HaveCount(2);
        ordered[0].Id.Should().Be(khobar.Id);
    }

    [Fact]
    public void Order_ShouldDropBranchFartherThanFiftyKm()
    {
        var khobar = new FakeBranch(Guid.NewGuid(), 26.22m, 50.19m, IsPrimary: false);
        var riyadh = new FakeBranch(Guid.NewGuid(), 24.71m, 46.67m, IsPrimary: true);
        var ordered = NearestBranchSelector.Order(
            [khobar, riyadh],
            26.2172m,
            50.1971m,
            latitude: b => b.Latitude,
            longitude: b => b.Longitude,
            isPrimary: b => b.IsPrimary,
            createdAtUtc: b => b.CreatedAtUtc).ToList();

        ordered.Should().ContainSingle().Which.Id.Should().Be(khobar.Id);
    }

    [Fact]
    public void Order_ShouldReturnEmptyWhenCustomerCoordinatesMissing()
    {
        var primary = new FakeBranch(Guid.NewGuid(), 26.43m, 50.08m, IsPrimary: true);
        var secondary = new FakeBranch(Guid.NewGuid(), 26.22m, 50.19m, IsPrimary: false);
        var ordered = NearestBranchSelector.Order(
            [primary, secondary],
            customerLatitude: null,
            customerLongitude: null,
            latitude: b => b.Latitude,
            longitude: b => b.Longitude,
            isPrimary: b => b.IsPrimary,
            createdAtUtc: b => b.CreatedAtUtc).ToList();

        ordered.Should().BeEmpty();
    }

    [Fact]
    public void Order_ShouldPreferCloserBranchOverPrimaryBranchFartherAway()
    {
        var farPrimary = new FakeBranch(Guid.NewGuid(), 26.62m, 50.19m, IsPrimary: true);
        var nearBranch = new FakeBranch(Guid.NewGuid(), 26.22m, 50.19m, IsPrimary: false);
        var ordered = NearestBranchSelector.Order(
            [farPrimary, nearBranch],
            customerLatitude: 26.2172m,
            customerLongitude: 50.1971m,
            latitude: b => b.Latitude,
            longitude: b => b.Longitude,
            isPrimary: b => b.IsPrimary,
            createdAtUtc: b => b.CreatedAtUtc).ToList();

        ordered.Should().HaveCount(2);
        ordered[0].Id.Should().Be(nearBranch.Id);
    }

    private sealed record FakeBranch(
        Guid Id,
        decimal Latitude,
        decimal Longitude,
        bool IsPrimary,
        DateTime CreatedAtUtc = default);
}
