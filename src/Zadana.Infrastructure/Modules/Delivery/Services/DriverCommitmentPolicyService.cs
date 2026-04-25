using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;

namespace Zadana.Infrastructure.Modules.Delivery.Services;

public class DriverCommitmentPolicyService : IDriverCommitmentPolicyService
{
    private const int DailyRejectionLimit = 3;
    private const int WeeklyRejectionLimit = 12;
    private const int WatchDailyThreshold = 2;
    private const int WatchWeeklyThreshold = 8;
    private const decimal RejectedPenalty = 18m;
    private const decimal TimedOutPenalty = 12m;
    private const decimal AcceptedBoost = 4m;
    private const decimal MaxAcceptedBoost = 20m;
    private static readonly TimeSpan DailyWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan WeeklyWindow = TimeSpan.FromDays(7);

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public DriverCommitmentPolicyService(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<DriverCommitmentSummaryDto> GetDriverSummaryAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var summaries = await GetDriverSummariesAsync([driverId], cancellationToken);
        return summaries.TryGetValue(driverId, out var summary)
            ? summary
            : CreateDefaultSummary();
    }

    public async Task<IReadOnlyDictionary<Guid, DriverCommitmentSummaryDto>> GetDriverSummariesAsync(
        IReadOnlyCollection<Guid> driverIds,
        CancellationToken cancellationToken = default)
    {
        if (driverIds.Count == 0)
        {
            return new Dictionary<Guid, DriverCommitmentSummaryDto>();
        }

        var distinctDriverIds = driverIds.Distinct().ToArray();
        var utcNow = DateTime.UtcNow;
        var weekWindowStart = utcNow.Subtract(WeeklyWindow);
        var dayWindowStart = utcNow.Subtract(DailyWindow);

        var attemptRows = await _context.DeliveryOfferAttempts
            .Where(item =>
                distinctDriverIds.Contains(item.DriverId) &&
                (item.RespondedAtUtc ?? item.OfferedAtUtc) >= weekWindowStart)
            .Select(item => new
            {
                item.DriverId,
                item.Status,
                EventAtUtc = item.RespondedAtUtc ?? item.OfferedAtUtc,
                item.RespondedAtUtc
            })
            .ToListAsync(cancellationToken);

        var groupedAttempts = attemptRows
            .GroupBy(item => item.DriverId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var result = new Dictionary<Guid, DriverCommitmentSummaryDto>(distinctDriverIds.Length);

        foreach (var driverId in distinctDriverIds)
        {
            groupedAttempts.TryGetValue(driverId, out var attempts);
            attempts ??= [];

            var acceptedOffers = attempts.Count(item => item.Status == DeliveryOfferAttemptStatus.Accepted);
            var rejectedOffers = attempts.Count(item => item.Status == DeliveryOfferAttemptStatus.Rejected);
            var timedOutOffers = attempts.Count(item => item.Status == DeliveryOfferAttemptStatus.TimedOut);

            var dailyRejections = attempts.Count(item =>
                item.EventAtUtc >= dayWindowStart &&
                item.Status is DeliveryOfferAttemptStatus.Rejected or DeliveryOfferAttemptStatus.TimedOut);

            var weeklyRejections = rejectedOffers + timedOutOffers;
            var acceptedBoost = Math.Min(MaxAcceptedBoost, acceptedOffers * AcceptedBoost);
            var commitmentScore = Math.Clamp(
                100m - (rejectedOffers * RejectedPenalty) - (timedOutOffers * TimedOutPenalty) + acceptedBoost,
                0m,
                100m);

            var softBlockedDaysInWeek = attempts
                .Where(item => item.Status is DeliveryOfferAttemptStatus.Rejected or DeliveryOfferAttemptStatus.TimedOut)
                .GroupBy(item => item.EventAtUtc.Date)
                .Count(group => group.Count() >= DailyRejectionLimit);

            var enforcementLevel = ResolveEnforcementLevel(
                dailyRejections,
                weeklyRejections,
                softBlockedDaysInWeek,
                commitmentScore);

            var canReceiveOffers = enforcementLevel is not (
                DriverCommitmentEnforcementLevel.SoftBlocked or
                DriverCommitmentEnforcementLevel.SuspensionCandidate);

            result[driverId] = new DriverCommitmentSummaryDto(
                AcceptedOffers: acceptedOffers,
                RejectedOffers: rejectedOffers,
                TimedOutOffers: timedOutOffers,
                DailyRejections: dailyRejections,
                WeeklyRejections: weeklyRejections,
                CommitmentScore: Math.Round(commitmentScore, 1),
                EnforcementLevel: enforcementLevel.ToString(),
                CanReceiveOffers: canReceiveOffers,
                RestrictionMessage: ResolveRestrictionMessage(enforcementLevel),
                LastOfferResponseAtUtc: attempts
                    .Where(item => item.RespondedAtUtc.HasValue)
                    .OrderByDescending(item => item.RespondedAtUtc)
                    .Select(item => item.RespondedAtUtc)
                    .FirstOrDefault());
        }

        return result;
    }

    public async Task ApplyOperationalEnforcementAsync(
        IReadOnlyCollection<Guid> driverIds,
        CancellationToken cancellationToken = default)
    {
        if (driverIds.Count == 0)
        {
            return;
        }

        var summaries = await GetDriverSummariesAsync(driverIds, cancellationToken);
        var blockedDriverIds = summaries
            .Where(item => !item.Value.CanReceiveOffers)
            .Select(item => item.Key)
            .ToArray();

        if (blockedDriverIds.Length == 0)
        {
            return;
        }

        var drivers = await _context.Drivers
            .Where(driver => blockedDriverIds.Contains(driver.Id))
            .ToListAsync(cancellationToken);

        var incidentCutoff = DateTime.UtcNow.Subtract(WeeklyWindow);
        var existingIncidents = await _context.DriverIncidents
            .Where(incident =>
                blockedDriverIds.Contains(incident.DriverId) &&
                incident.IncidentType == "offer-compliance" &&
                incident.CreatedAtUtc >= incidentCutoff)
            .ToListAsync(cancellationToken);

        var hasChanges = false;

        foreach (var driver in drivers)
        {
            if (driver.IsAvailable)
            {
                driver.ToggleAvailability(false);
                hasChanges = true;
            }

            if (!summaries.TryGetValue(driver.Id, out var summary) ||
                summary.EnforcementLevel != DriverCommitmentEnforcementLevel.SuspensionCandidate.ToString())
            {
                continue;
            }

            var alreadyTracked = existingIncidents.Any(incident =>
                incident.DriverId == driver.Id &&
                incident.Status != DriverIncidentStatus.Resolved);

            if (alreadyTracked)
            {
                continue;
            }

            _context.DriverIncidents.Add(new DriverIncident(
                driver.Id,
                "offer-compliance",
                DriverIncidentSeverity.High,
                "Driver repeatedly exceeded offer rejection or timeout thresholds within the rolling 7-day window."));

            hasChanges = true;
        }

        if (hasChanges)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static DriverCommitmentSummaryDto CreateDefaultSummary() =>
        new(
            AcceptedOffers: 0,
            RejectedOffers: 0,
            TimedOutOffers: 0,
            DailyRejections: 0,
            WeeklyRejections: 0,
            CommitmentScore: 100m,
            EnforcementLevel: DriverCommitmentEnforcementLevel.Healthy.ToString(),
            CanReceiveOffers: true,
            RestrictionMessage: null,
            LastOfferResponseAtUtc: null);

    private static DriverCommitmentEnforcementLevel ResolveEnforcementLevel(
        int dailyRejections,
        int weeklyRejections,
        int softBlockedDaysInWeek,
        decimal commitmentScore)
    {
        var softBlocked = dailyRejections >= DailyRejectionLimit || weeklyRejections >= WeeklyRejectionLimit;
        if (softBlocked && softBlockedDaysInWeek >= 2)
        {
            return DriverCommitmentEnforcementLevel.SuspensionCandidate;
        }

        if (softBlocked)
        {
            return DriverCommitmentEnforcementLevel.SoftBlocked;
        }

        if (dailyRejections >= WatchDailyThreshold || weeklyRejections >= WatchWeeklyThreshold || commitmentScore <= 80m)
        {
            return DriverCommitmentEnforcementLevel.Watch;
        }

        return DriverCommitmentEnforcementLevel.Healthy;
    }

    private static string? ResolveRestrictionMessage(DriverCommitmentEnforcementLevel enforcementLevel) =>
        enforcementLevel switch
        {
            DriverCommitmentEnforcementLevel.SoftBlocked =>
                "Driver exceeded the daily or weekly offer rejection limit and is temporarily blocked from receiving new offers.",
            DriverCommitmentEnforcementLevel.SuspensionCandidate =>
                "Driver repeatedly exceeded offer rejection limits and now requires admin review before resuming normal dispatch priority.",
            _ => null
        };
}
