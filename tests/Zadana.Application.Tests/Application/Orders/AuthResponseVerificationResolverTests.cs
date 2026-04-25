using FluentAssertions;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Tests.Application.Orders;

public class AuthResponseVerificationResolverTests
{
    [Fact]
    public void Resolve_WhenDriverIsUnderReview_ShouldReturnFalse()
    {
        var driverStatus = new DriverOperationalStatusDto(
            DriverId: Guid.NewGuid(),
            GateStatus: "UnderReview",
            IsOperational: false,
            CanReceiveOrders: false,
            CanGoAvailable: false,
            IsAvailable: false,
            VerificationStatus: "UnderReview",
            AccountStatus: "Pending",
            ReviewedAtUtc: null,
            ReviewNote: null,
            SuspensionReason: null,
            PrimaryZoneId: null,
            ZoneName: null,
            CommitmentScore: 74m,
            DailyRejections: 2,
            WeeklyRejections: 2,
            EnforcementLevel: "Watch",
            CanReceiveOffers: true,
            RestrictionMessage: null,
            Message: "Driver profile is currently under admin review.");

        var result = AuthResponseVerificationResolver.Resolve(UserRole.Driver, driverStatus);

        result.Should().BeFalse();
    }

    [Fact]
    public void Resolve_WhenDriverIsOperational_ShouldReturnTrue()
    {
        var driverStatus = new DriverOperationalStatusDto(
            DriverId: Guid.NewGuid(),
            GateStatus: "Operational",
            IsOperational: true,
            CanReceiveOrders: true,
            CanGoAvailable: true,
            IsAvailable: true,
            VerificationStatus: "Approved",
            AccountStatus: "Active",
            ReviewedAtUtc: DateTime.UtcNow,
            ReviewNote: "Approved",
            SuspensionReason: null,
            PrimaryZoneId: Guid.NewGuid(),
            ZoneName: "Nasr City",
            CommitmentScore: 100m,
            DailyRejections: 0,
            WeeklyRejections: 0,
            EnforcementLevel: "Healthy",
            CanReceiveOffers: true,
            RestrictionMessage: null,
            Message: "Driver is operational.");

        var result = AuthResponseVerificationResolver.Resolve(UserRole.Driver, driverStatus);

        result.Should().BeTrue();
    }

    [Fact]
    public void Resolve_WhenUserIsNotDriver_ShouldReturnTrue()
    {
        var result = AuthResponseVerificationResolver.Resolve(UserRole.Customer, driverStatus: null);

        result.Should().BeTrue();
    }
}
