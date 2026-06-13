using Zadana.Application.Modules.Delivery.DTOs;

namespace Zadana.Application.Modules.Delivery.Support;

public static class DriverHomeStateResolver
{
    public static string Resolve(
        DriverOperationalStatusDto operationalStatus,
        DriverIncomingOfferDto? currentOffer,
        DriverCurrentAssignmentDto? currentAssignment)
    {
        if (currentAssignment is not null)
        {
            return "OnMission";
        }

        if (!operationalStatus.IsOperational)
        {
            if (!operationalStatus.CanReceiveOffers &&
                string.Equals(operationalStatus.GateStatus, "Operational", StringComparison.Ordinal))
            {
                return ResolveCommitmentHomeState(operationalStatus.EnforcementLevel);
            }

            return operationalStatus.GateStatus;
        }

        if (currentOffer is not null)
        {
            return "IncomingOffer";
        }

        return operationalStatus.IsAvailable ? "WaitingForOffer" : "Offline";
    }

    private static string ResolveCommitmentHomeState(string enforcementLevel) =>
        string.Equals(enforcementLevel, "SuspensionCandidate", StringComparison.OrdinalIgnoreCase)
            ? "SuspensionCandidate"
            : "SoftBlocked";
}
