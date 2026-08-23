using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.SharedKernel.Serialization;
using Zadana.Domain.Modules.Social.Enums;

namespace Zadana.Infrastructure.Modules.Delivery.Services;

public class DriverCommitmentPolicyService : IDriverCommitmentPolicyService
{
    private const int DailyRejectionLimit = 3;
    private const int DailyCancellationLimit = 2;
    private const int WeeklyRejectionLimit = 20;
    private const int WeeklyCancellationReviewThreshold = 5;
    private const int WatchDailyThreshold = 2;
    private const int WatchWeeklyThreshold = 8;
    private const decimal RejectedPenalty = 18m;
    private const decimal TimedOutPenalty = 12m;
    private const decimal AcceptedBoost = 4m;
    private const decimal MaxAcceptedBoost = 20m;
    private static readonly TimeSpan WeeklyWindow = TimeSpan.FromDays(7);

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public DriverCommitmentPolicyService(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork)
        : this(
            context,
            unitOfWork,
            NoOpNotificationService.Instance,
            NoOpOneSignalPushService.Instance)
    {
    }

    public DriverCommitmentPolicyService(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
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
        var todayStart = SaudiTime.StartOfTodayUtc;

        var commitmentClearDates = await _context.Drivers
            .Where(driver => distinctDriverIds.Contains(driver.Id))
            .Select(driver => new
            {
                driver.Id,
                driver.CommitmentClearedAtUtc
            })
            .ToDictionaryAsync(item => item.Id, item => item.CommitmentClearedAtUtc, cancellationToken);

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

        var cancellationRows = await _context.DeliveryAssignments
            .Where(item =>
                item.DriverId.HasValue &&
                distinctDriverIds.Contains(item.DriverId.Value) &&
                item.FailedAtUtc.HasValue &&
                item.FailedAtUtc.Value >= weekWindowStart &&
                (item.Status == AssignmentStatus.Failed ||
                 (item.Status == AssignmentStatus.Cancelled &&
                  item.FailureReason != null &&
                  item.FailureReason.Contains("driver"))))
            .Select(item => new
            {
                DriverId = item.DriverId!.Value,
                EventAtUtc = item.FailedAtUtc!.Value
            })
            .ToListAsync(cancellationToken);

        var groupedCancellations = cancellationRows
            .GroupBy(item => item.DriverId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var result = new Dictionary<Guid, DriverCommitmentSummaryDto>(distinctDriverIds.Length);

        foreach (var driverId in distinctDriverIds)
        {
            groupedAttempts.TryGetValue(driverId, out var attempts);
            attempts ??= [];
            commitmentClearDates.TryGetValue(driverId, out var commitmentClearedAtUtc);
            if (commitmentClearedAtUtc.HasValue)
            {
                attempts = attempts
                    .Where(item => item.EventAtUtc >= commitmentClearedAtUtc.Value)
                    .ToArray();
            }

            groupedCancellations.TryGetValue(driverId, out var cancellations);
            cancellations ??= [];
            if (commitmentClearedAtUtc.HasValue)
            {
                cancellations = cancellations
                    .Where(item => item.EventAtUtc >= commitmentClearedAtUtc.Value)
                    .ToArray();
            }

            var acceptedOffers = attempts.Count(item => item.Status == DeliveryOfferAttemptStatus.Accepted);
            var rejectedOffers = attempts.Count(item => item.Status == DeliveryOfferAttemptStatus.Rejected);
            var timedOutOffers = attempts.Count(item => item.Status == DeliveryOfferAttemptStatus.TimedOut);

            var dailyRejections = attempts.Count(item =>
                item.EventAtUtc >= todayStart &&
                item.Status is DeliveryOfferAttemptStatus.Rejected or DeliveryOfferAttemptStatus.TimedOut);
            var dailyCancellations = cancellations.Count(item => item.EventAtUtc >= todayStart);

            var weeklyRejections = rejectedOffers + timedOutOffers;
            var weeklyCancellations = cancellations.Length;
            var acceptedBoost = Math.Min(MaxAcceptedBoost, acceptedOffers * AcceptedBoost);
            var commitmentScore = Math.Clamp(
                100m - (rejectedOffers * RejectedPenalty) - (timedOutOffers * TimedOutPenalty) - (weeklyCancellations * RejectedPenalty) + acceptedBoost,
                0m,
                100m);

            var softBlockedDaysInWeek = attempts
                .Where(item => item.Status is DeliveryOfferAttemptStatus.Rejected or DeliveryOfferAttemptStatus.TimedOut)
                .GroupBy(item => item.EventAtUtc.Date)
                .Count(group => group.Count() >= DailyRejectionLimit);
            var cancellationBlockedDaysInWeek = cancellations
                .GroupBy(item => item.EventAtUtc.Date)
                .Count(group => group.Count() >= DailyCancellationLimit);

            var enforcementLevel = ResolveEnforcementLevel(
                dailyRejections,
                weeklyRejections,
                softBlockedDaysInWeek + cancellationBlockedDaysInWeek,
                dailyCancellations,
                weeklyCancellations,
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
                RestrictionMessage: ResolveRestrictionMessageArClean(enforcementLevel),
                LastOfferResponseAtUtc: attempts
                    .Where(item => item.RespondedAtUtc.HasValue)
                    .OrderByDescending(item => item.RespondedAtUtc)
                    .Select(item => item.RespondedAtUtc)
                    .FirstOrDefault(),
                RestrictionMessageEn: ResolveRestrictionMessageEn(enforcementLevel));
        }

        return result;
    }

    public static int GetDailyRejectionLimit() => DailyRejectionLimit;

    public static int GetWeeklyRejectionLimit() => WeeklyRejectionLimit;

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
        var notifications = new List<(Driver Driver, DriverCommitmentSummaryDto Summary, bool ForcedOffline, bool SuspensionCandidateTriggered)>();

        foreach (var driver in drivers)
        {
            var forcedOffline = false;
            if (driver.IsAvailable)
            {
                driver.ToggleAvailability(false);
                hasChanges = true;
                forcedOffline = true;
            }

            if (!summaries.TryGetValue(driver.Id, out var summary) ||
                summary.EnforcementLevel != DriverCommitmentEnforcementLevel.SuspensionCandidate.ToString())
            {
                if (forcedOffline && summaries.TryGetValue(driver.Id, out var softBlockedSummary))
                {
                    notifications.Add((driver, softBlockedSummary, true, false));
                }

                continue;
            }

            var alreadyTracked = existingIncidents.Any(incident =>
                incident.DriverId == driver.Id &&
                incident.Status != DriverIncidentStatus.Resolved);

            if (alreadyTracked)
            {
                if (forcedOffline)
                {
                    notifications.Add((driver, summary, true, false));
                }

                continue;
            }

            _context.DriverIncidents.Add(new DriverIncident(
                driver.Id,
                "offer-compliance",
                DriverIncidentSeverity.High,
                "Driver repeatedly exceeded offer rejection or timeout thresholds within the rolling 7-day window."));

            hasChanges = true;
            notifications.Add((driver, summary, forcedOffline, true));
        }

        if (!hasChanges)
        {
            foreach (var notification in notifications)
            {
                await SendCommitmentNotificationAsync(notification.Driver, notification.Summary, notification.ForcedOffline, notification.SuspensionCandidateTriggered, cancellationToken);
            }

            return;
        }

        if (hasChanges)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        foreach (var notification in notifications)
        {
            await SendCommitmentNotificationAsync(notification.Driver, notification.Summary, notification.ForcedOffline, notification.SuspensionCandidateTriggered, cancellationToken);
        }
    }

    private async Task SendCommitmentNotificationAsync(
        Driver driver,
        DriverCommitmentSummaryDto summary,
        bool forcedOffline,
        bool suspensionCandidateTriggered,
        CancellationToken cancellationToken)
    {
        var eventName = suspensionCandidateTriggered
            ? "performance.suspension_candidate"
            : summary.EnforcementLevel == DriverCommitmentEnforcementLevel.SoftBlocked.ToString()
                ? "performance.soft_blocked"
                : "performance.forced_offline";

        var titleAr = suspensionCandidateTriggered
            ? "مراجعة تشغيلية مطلوبة"
            : "الحساب محظور مؤقتًا";
        var titleEn = suspensionCandidateTriggered
            ? "Operational review required"
            : "Account temporarily restricted";
        var bodyAr = summary.RestrictionMessage
            ?? (suspensionCandidateTriggered
                ? "قيّدنا الحساب مؤقتًا بسبب تكرار رفض العروض أو إلغاء التوصيل. فضلاً انتظر مراجعة الإدارة قبل استقبال الطلبات مجددًا."
                : "قيّدنا الحساب مؤقتًا بعد الوصول إلى حد رفض العروض أو إلغاء التوصيل اليوم. تقدر تستقبل العروض مجددًا غدًا أو بعد رفع التقييد من الإدارة.");
        var bodyEn = summary.RestrictionMessageEn
            ?? (suspensionCandidateTriggered
                ? "Your account was temporarily restricted because offer rejections or delivery cancellations occurred repeatedly. Please wait for admin review before receiving orders again."
                : "Your account was temporarily restricted after reaching today's offer rejection or delivery cancellation limit. You can receive offers again tomorrow or after admin clearance.");

        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: eventName,
            driverId: driver.Id,
            extra: new
            {
                enforcementLevel = summary.EnforcementLevel,
                commitmentScore = summary.CommitmentScore,
                dailyRejections = summary.DailyRejections,
                weeklyRejections = summary.WeeklyRejections,
                canReceiveOffers = summary.CanReceiveOffers,
                isFrozen = !summary.CanReceiveOffers,
                forcedOffline
            });

        await _notificationService.SendToUserAsync(
            driver.UserId,
            new NotificationDispatchRequest(
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                NotificationTypes.DriverCommitmentEnforcement,
                NotificationCategories.Account,
                NotificationPriorities.High,
                driver.Id,
                data),
            cancellationToken);

        await _notificationService.SendDriverHomeUpdatedAsync(driver.UserId, cancellationToken);

        await _oneSignalPushService.SendMobileNotificationAsync(
            OneSignalMobilePushRequest.CreateHeadsUp(
                driver.UserId.ToString(),
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                NotificationTypes.DriverCommitmentEnforcement,
                driver.Id,
                data,
                targetUrl: "/account-status",
                category: NotificationCategories.Account,
                targetApplication: OneSignalApplicationTarget.Driver),
            cancellationToken);
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
            LastOfferResponseAtUtc: null,
            RestrictionMessageEn: null);

    private static DriverCommitmentEnforcementLevel ResolveEnforcementLevel(
        int dailyRejections,
        int weeklyRejections,
        int softBlockedDaysInWeek,
        int dailyCancellations,
        int weeklyCancellations,
        decimal commitmentScore)
    {
        var dailyBlocked = dailyRejections >= DailyRejectionLimit || dailyCancellations >= DailyCancellationLimit;
        if (dailyBlocked &&
            (softBlockedDaysInWeek >= 2 ||
             weeklyRejections >= WeeklyRejectionLimit ||
             weeklyCancellations >= WeeklyCancellationReviewThreshold))
        {
            return DriverCommitmentEnforcementLevel.SuspensionCandidate;
        }

        if (dailyBlocked)
        {
            return DriverCommitmentEnforcementLevel.SoftBlocked;
        }

        if (dailyRejections >= WatchDailyThreshold ||
            dailyCancellations > 0 ||
            weeklyRejections >= WatchWeeklyThreshold ||
            weeklyCancellations >= 2 ||
            commitmentScore <= 80m)
        {
            return DriverCommitmentEnforcementLevel.Watch;
        }

        return DriverCommitmentEnforcementLevel.Healthy;
    }

    private static string? ResolveRestrictionMessageAr(DriverCommitmentEnforcementLevel enforcementLevel) =>
        enforcementLevel switch
        {
            DriverCommitmentEnforcementLevel.SoftBlocked =>
                "قيّدنا الحساب مؤقتًا بعد الوصول إلى حد رفض العروض أو إلغاء التوصيل لليوم. تقدر تستقبل العروض مجددًا غدًا أو بعد رفع القيد من الإدارة.",
            DriverCommitmentEnforcementLevel.SuspensionCandidate =>
                "قيّدنا الحساب مؤقتًا بسبب تكرار رفض العروض أو إلغاء التوصيل. فضلاً انتظر مراجعة الإدارة قبل استقبال الطلبات مجددًا.",
            _ => null
        };

    private static string? ResolveRestrictionMessageEn(DriverCommitmentEnforcementLevel enforcementLevel) =>
        enforcementLevel switch
        {
            DriverCommitmentEnforcementLevel.SoftBlocked =>
                "Your account was temporarily restricted after reaching today's offer rejection or delivery cancellation limit. You can receive offers again tomorrow or after admin clearance.",
            DriverCommitmentEnforcementLevel.SuspensionCandidate =>
                "Your account was temporarily restricted because offer rejections or delivery cancellations happened repeatedly. Please wait for admin review before receiving orders again.",
            _ => null
        };

    private static string? ResolveRestrictionMessageArClean(DriverCommitmentEnforcementLevel enforcementLevel) =>
        ResolveRestrictionMessageAr(enforcementLevel);

    private sealed class NoOpNotificationService : INotificationService
    {
        public static readonly NoOpNotificationService Instance = new();

        public Task PersistToUserAsync(
            Guid userId,
            NotificationDispatchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PersistToUserAsync(
            Guid userId,
            string titleAr,
            string titleEn,
            string bodyAr,
            string bodyEn,
            string? type = null,
            Guid? referenceId = null,
            string? data = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendToUserAsync(
            Guid userId,
            NotificationDispatchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendToUserAsync(
            Guid userId,
            string titleAr,
            string titleEn,
            string bodyAr,
            string bodyEn,
            string? type = null,
            Guid? referenceId = null,
            string? data = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendOrderStatusChangedToUserAsync(
            Guid userId,
            Guid orderId,
            string orderNumber,
            Guid vendorId,
            string oldStatus,
            string newStatus,
            string? actorRole = null,
            string? action = null,
            string? targetUrl = null,
            CancellationToken cancellationToken = default,
            string? fulfillmentType = null,
            bool showPopup = false) =>
            Task.CompletedTask;

        public Task SendDriverArrivalStateChangedToUserAsync(
            Guid userId,
            Guid orderId,
            string orderNumber,
            string arrivalState,
            string driverName,
            string? actorRole = null,
            string? targetUrl = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendOrderSupportCaseChangedToUserAsync(
            Guid userId,
            Guid caseId,
            Guid orderId,
            string orderNumber,
            string type,
            string status,
            string action,
            string? targetUrl = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendDriverSupportCaseChangedToUserAsync(
            Guid driverUserId,
            Guid caseId,
            Guid? driverId,
            Guid? orderId,
            string? orderNumber,
            string type,
            string status,
            string action,
            string? targetUrl = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendDeliveryOfferToDriverAsync(
            Guid driverUserId,
            Application.Modules.Delivery.DTOs.DriverIncomingOfferDto currentOffer,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task BroadcastToAllCustomersAsync(
            string titleAr,
            string titleEn,
            string bodyAr,
            string bodyEn,
            string? type = null,
            string? data = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendAssignmentUpdatedToDriverAsync(
            Guid driverUserId,
            Guid assignmentId,
            Guid orderId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendDriverHomeUpdatedAsync(
            Guid driverUserId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendDriverWalletUpdatedAsync(
            Guid driverUserId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpOneSignalPushService : IOneSignalPushService
    {
        private static readonly OneSignalPushDispatchResult SkippedResult = new(
            Attempted: false,
            Sent: false,
            Skipped: true,
            ProviderStatusCode: null,
            ProviderNotificationId: null,
            Reason: "noop");

        public static readonly NoOpOneSignalPushService Instance = new();

        public Task<OneSignalPushDispatchResult> SendMobileNotificationAsync(
            OneSignalMobilePushRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SkippedResult);

        public Task<OneSignalPushDispatchResult> SendMobileNotificationDirectAsync(
            OneSignalMobilePushRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SkippedResult);

        public Task<OneSignalPushDispatchResult> SendToExternalUserAsync(
            string externalUserId,
            string titleAr,
            string titleEn,
            string bodyAr,
            string bodyEn,
            string? type = null,
            Guid? referenceId = null,
            string? data = null,
            string? targetUrl = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SkippedResult);

        public Task<OneSignalPushDispatchResult> SendToExternalUserAsync(
            string externalUserId,
            string titleAr,
            string titleEn,
            string bodyAr,
            string bodyEn,
            string? type,
            Guid? referenceId,
            string? data,
            string? targetUrl,
            OneSignalPushProfile profile,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SkippedResult);

        public Task<IReadOnlyList<OneSignalPushDispatchResult>> SendToExternalUsersAsync(
            IReadOnlyCollection<string> externalUserIds,
            string titleAr,
            string titleEn,
            string bodyAr,
            string bodyEn,
            string? type = null,
            Guid? referenceId = null,
            string? data = null,
            string? targetUrl = null,
            OneSignalPushProfile profile = OneSignalPushProfile.Default,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OneSignalPushDispatchResult>>([]);

        public Task<IReadOnlyList<OneSignalPushDispatchResult>> SendToExternalUsersAsync(
            IReadOnlyCollection<string> externalUserIds,
            string titleAr,
            string titleEn,
            string bodyAr,
            string bodyEn,
            string? type,
            Guid? referenceId,
            string? data,
            string? targetUrl,
            OneSignalPushProfile profile,
            OneSignalApplicationTarget targetApplication,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OneSignalPushDispatchResult>>([]);
    }
}
