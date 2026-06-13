using FluentAssertions;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Support;

namespace Zadana.Application.Tests.Application.Orders;

public class DriverHomeStateResolverTests
{
    [Fact]
    public void Resolve_WhenCommitmentSoftBlocked_ShouldReturnSoftBlockedNotOperational()
    {
        var operationalStatus = CreateOperationalStatus(
            gateStatus: "Operational",
            isOperational: false,
            canReceiveOffers: false,
            enforcementLevel: "SoftBlocked");

        var homeState = DriverHomeStateResolver.Resolve(operationalStatus, currentOffer: null, currentAssignment: null);

        homeState.Should().Be("SoftBlocked");
    }

    [Fact]
    public void Resolve_WhenCommitmentSuspensionCandidate_ShouldReturnSuspensionCandidate()
    {
        var operationalStatus = CreateOperationalStatus(
            gateStatus: "Operational",
            isOperational: false,
            canReceiveOffers: false,
            enforcementLevel: "SuspensionCandidate");

        var homeState = DriverHomeStateResolver.Resolve(operationalStatus, currentOffer: null, currentAssignment: null);

        homeState.Should().Be("SuspensionCandidate");
    }

    [Fact]
    public void Resolve_WhenOperationalAndCanReceiveOffers_ShouldReturnWaitingForOffer()
    {
        var operationalStatus = CreateOperationalStatus(
            gateStatus: "Operational",
            isOperational: true,
            canReceiveOffers: true,
            isAvailable: true,
            enforcementLevel: "Healthy");

        var homeState = DriverHomeStateResolver.Resolve(operationalStatus, currentOffer: null, currentAssignment: null);

        homeState.Should().Be("WaitingForOffer");
    }

    [Fact]
    public void Resolve_WhenSuspended_ShouldKeepSuspendedGateStatus()
    {
        var operationalStatus = CreateOperationalStatus(
            gateStatus: "Suspended",
            isOperational: false,
            canReceiveOffers: false,
            enforcementLevel: "SoftBlocked");

        var homeState = DriverHomeStateResolver.Resolve(operationalStatus, currentOffer: null, currentAssignment: null);

        homeState.Should().Be("Suspended");
    }

    private static DriverOperationalStatusDto CreateOperationalStatus(
        string gateStatus,
        bool isOperational,
        bool canReceiveOffers,
        string enforcementLevel,
        bool isAvailable = false) =>
        new(
            DriverId: Guid.NewGuid(),
            GateStatus: gateStatus,
            IsOperational: isOperational,
            CanReceiveOrders: canReceiveOffers,
            CanGoAvailable: canReceiveOffers,
            IsAvailable: isAvailable,
            VerificationStatus: "Approved",
            AccountStatus: "Active",
            ReviewedAtUtc: null,
            ReviewNote: null,
            SuspensionReason: null,
            CommitmentScore: 100m,
            DailyRejections: 0,
            WeeklyRejections: 0,
            EnforcementLevel: enforcementLevel,
            CanReceiveOffers: canReceiveOffers,
            RestrictionMessage: null,
            Message: string.Empty);
}
