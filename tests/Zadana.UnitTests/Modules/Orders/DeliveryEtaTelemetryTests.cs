using FluentAssertions;
using Zadana.Application.Modules.Orders.Support;

namespace Zadana.UnitTests.Modules.Orders;

public class DeliveryEtaTelemetryTests
{
    [Fact]
    public void BuildOperationalProfile_WhenSnapshotsExist_ComputesAcceptancePercentilesAndCoverage()
    {
        var baseTime = new DateTime(2026, 05, 17, 16, 0, 0, DateTimeKind.Utc);
        var snapshots = Enumerable.Range(0, 6)
            .Select(index =>
            {
                var placedAt = baseTime.AddDays(-index).AddMinutes(-(45 + index));
                return new DeliveryEtaHistoricalSnapshot(
                    Guid.NewGuid(),
                    placedAt,
                    placedAt.AddMinutes(45 + index),
                    placedAt.AddMinutes(4 + (index % 2)),
                    placedAt.AddMinutes(7 + (index % 2)),
                    placedAt.AddMinutes(22 + index),
                    placedAt.AddMinutes(27 + index),
                    placedAt.AddMinutes(33 + index),
                    placedAt.AddMinutes(28 + index),
                    placedAt.AddMinutes(33 + index));
            })
            .ToArray();

        var profile = DeliveryEtaTelemetry.BuildOperationalProfile(snapshots, "branch_historical");

        profile.CalibrationSource.Should().Be("branch_historical");
        profile.SampleSize.Should().Be(6);
        profile.AverageAcceptanceMinutes.Should().BeGreaterThan(3d);
        profile.Percentile80TotalMinutes.Should().BeGreaterThanOrEqualTo(profile.Percentile50TotalMinutes);
        profile.StageCoverageRatio.Should().BeGreaterThan(0.75m);
        profile.IsReliable.Should().BeTrue();
    }

    [Fact]
    public void BuildOperationalProfile_WhenSnapshotsMissingStages_RemainsUsableButNotReliable()
    {
        var placedAt = new DateTime(2026, 05, 17, 12, 0, 0, DateTimeKind.Utc);
        var snapshots = Enumerable.Range(0, 4)
            .Select(index => new DeliveryEtaHistoricalSnapshot(
                Guid.NewGuid(),
                placedAt.AddDays(-index).AddMinutes(-40),
                placedAt.AddDays(-index),
                null,
                null,
                null,
                null,
                placedAt.AddDays(-index).AddMinutes(-10),
                null,
                null))
            .ToArray();

        var profile = DeliveryEtaTelemetry.BuildOperationalProfile(snapshots, "vendor_historical");

        profile.SampleSize.Should().Be(4);
        profile.IsReliable.Should().BeFalse();
        profile.StageCoverageRatio.Should().BeLessThan(0.55m);
        profile.RecommendedBufferMinutes.Should().BeGreaterThanOrEqualTo(8);
    }
}
