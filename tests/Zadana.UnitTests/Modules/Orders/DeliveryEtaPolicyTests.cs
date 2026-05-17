using FluentAssertions;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.UnitTests.Modules.Orders;

public class DeliveryEtaPolicyTests
{
    [Fact]
    public void EstimateCheckoutWindow_WhenBranchProfileIsReliable_IncludesHistoricalSourceAndOperationalRange()
    {
        var profile = new DeliveryEtaOperationalProfile(
            CalibrationSource: "branch_historical",
            SampleSize: 8,
            AverageTotalMinutes: 44d,
            AverageAcceptanceMinutes: 4d,
            AveragePreparationMinutes: 16d,
            AverageDispatchLeadMinutes: 9d,
            AverageLastMileMinutes: 13d,
            Percentile50TotalMinutes: 42d,
            Percentile80TotalMinutes: 51d,
            RecommendedBufferMinutes: 9,
            OnTimeRate: 87m,
            StageCoverageRatio: 0.84m,
            IsReliable: true);

        var window = DeliveryEtaPolicy.EstimateCheckoutWindow(18, 1.5m, 3m, profile);

        window.Source.Should().Be("branch_historical");
        window.Confidence.Should().Be("high");
        window.IsApproximate.Should().BeFalse();
        window.MaxMinutes.Should().BeGreaterThan(window.MinMinutes);
        window.MinMinutes.Should().BeGreaterThanOrEqualTo(35);
    }

    [Fact]
    public void EstimateCheckoutWindow_WhenNoHistoryExists_FallsBackToDistanceBaseline()
    {
        var window = DeliveryEtaPolicy.EstimateCheckoutWindow(null, 2m, 4m, DeliveryEtaOperationalProfile.Default);

        window.Source.Should().Be("distance_baseline");
        window.Confidence.Should().Be("low");
        window.IsApproximate.Should().BeTrue();
        window.MaxMinutes.Should().BeGreaterThan(window.MinMinutes);
    }

    [Fact]
    public void EstimateTracking_WhenOrderIsAccepted_RemovesAcceptanceLagFromRemainingTime()
    {
        var profile = new DeliveryEtaOperationalProfile(
            CalibrationSource: "vendor_historical",
            SampleSize: 6,
            AverageTotalMinutes: 48d,
            AverageAcceptanceMinutes: 6d,
            AveragePreparationMinutes: 18d,
            AverageDispatchLeadMinutes: 10d,
            AverageLastMileMinutes: 14d,
            Percentile50TotalMinutes: 46d,
            Percentile80TotalMinutes: 58d,
            RecommendedBufferMinutes: 10,
            OnTimeRate: 82m,
            StageCoverageRatio: 0.78m,
            IsReliable: true);

        var placedAt = DateTime.UtcNow.AddMinutes(-8);
        var acceptedAt = placedAt.AddMinutes(4);

        var pending = DeliveryEtaPolicy.EstimateTracking(
            OrderStatus.PendingVendorAcceptance,
            placedAt,
            null,
            acceptedAt,
            null,
            null,
            20,
            1m,
            3m,
            profile);

        var accepted = DeliveryEtaPolicy.EstimateTracking(
            OrderStatus.Accepted,
            placedAt,
            null,
            acceptedAt,
            null,
            null,
            20,
            1m,
            3m,
            profile);

        pending.Should().NotBeNull();
        accepted.Should().NotBeNull();
        accepted!.Window!.Source.Should().Be("vendor_historical");
        accepted.Window.Confidence.Should().Be("high");
        accepted.Window.MaxMinutes.Should().BeLessThan(pending!.Window!.MaxMinutes);
    }

    [Fact]
    public void EstimateTracking_WhenDriverIsAssigned_UsesLiveOperationalSource()
    {
        var profile = new DeliveryEtaOperationalProfile(
            CalibrationSource: "branch_historical",
            SampleSize: 7,
            AverageTotalMinutes: 41d,
            AverageAcceptanceMinutes: 4d,
            AveragePreparationMinutes: 15d,
            AverageDispatchLeadMinutes: 8d,
            AverageLastMileMinutes: 12d,
            Percentile50TotalMinutes: 39d,
            Percentile80TotalMinutes: 49d,
            RecommendedBufferMinutes: 8,
            OnTimeRate: 90m,
            StageCoverageRatio: 0.81m,
            IsReliable: true);

        var estimate = DeliveryEtaPolicy.EstimateTracking(
            OrderStatus.DriverAssigned,
            DateTime.UtcNow.AddMinutes(-20),
            null,
            DateTime.UtcNow.AddMinutes(-16),
            DateTime.UtcNow.AddMinutes(-4),
            null,
            18,
            1.2m,
            2.8m,
            profile);

        estimate.Should().NotBeNull();
        estimate!.Window!.Source.Should().Be("live_operational");
        estimate.Window.Confidence.Should().Be("high");
        estimate.Window.IsApproximate.Should().BeFalse();
    }
}
