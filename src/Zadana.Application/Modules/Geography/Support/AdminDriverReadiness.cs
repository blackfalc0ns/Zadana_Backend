using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Geography.Support;

public static class AdminDriverReadiness
{
    public static bool IsReady(
        AccountStatus status,
        DriverVerificationStatus verificationStatus,
        bool isAvailable,
        bool isLocationUpdatesBlocked) =>
        ResolveReadiness(status, verificationStatus, isAvailable, isLocationUpdatesBlocked) == "ready";

    public static bool IsVerifiedActive(
        AccountStatus status,
        DriverVerificationStatus verificationStatus,
        bool isLocationUpdatesBlocked) =>
        status == AccountStatus.Active
        && verificationStatus == DriverVerificationStatus.Approved
        && !isLocationUpdatesBlocked;

    public static string ResolveReadiness(
        AccountStatus status,
        DriverVerificationStatus verificationStatus,
        bool isAvailable,
        bool isLocationUpdatesBlocked)
    {
        if (status is AccountStatus.Suspended or AccountStatus.Banned
            || verificationStatus == DriverVerificationStatus.Rejected
            || isLocationUpdatesBlocked)
        {
            return "blocked";
        }

        if (status != AccountStatus.Active
            || verificationStatus != DriverVerificationStatus.Approved
            || !isAvailable)
        {
            return "limited";
        }

        return "ready";
    }
}
