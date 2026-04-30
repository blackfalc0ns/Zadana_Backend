using Zadana.Domain.Modules.Delivery.Enums;

namespace Zadana.Application.Modules.Delivery.DTOs;

public record DeliveryZoneDto(
    Guid Id,
    string City,
    string Name,
    decimal CenterLat,
    decimal CenterLng,
    decimal RadiusKm,
    bool IsActive);

public record DriverOperationalStatusDto(
    Guid DriverId,
    string GateStatus,
    bool IsOperational,
    bool CanReceiveOrders,
    bool CanGoAvailable,
    bool IsAvailable,
    string VerificationStatus,
    string AccountStatus,
    DateTime? ReviewedAtUtc,
    string? ReviewNote,
    string? SuspensionReason,

    decimal CommitmentScore,
    int DailyRejections,
    int WeeklyRejections,
    string EnforcementLevel,
    bool CanReceiveOffers,
    string? RestrictionMessage,
    string Message);

public record DriverCommitmentSummaryDto(
    int AcceptedOffers,
    int RejectedOffers,
    int TimedOutOffers,
    int DailyRejections,
    int WeeklyRejections,
    decimal CommitmentScore,
    string EnforcementLevel,
    bool CanReceiveOffers,
    string? RestrictionMessage,
    DateTime? LastOfferResponseAtUtc);

public record DriverHomeDto(
    DriverOperationalStatusDto OperationalStatus,
    string HomeState,
    DriverIncomingOfferDto? CurrentOffer,
    DriverCurrentAssignmentDto? CurrentAssignment,
    DriverEarningsSummaryDto EarningsSummaryToday,
    int UnreadAlerts,
    DriverCommitmentSummaryDto Commitment,
    DriverHomeProfileReadinessDto ProfileReadiness);

public record DriverHomeProfileReadinessDto(
    bool IsProfileComplete,
    int CompletionPercent,
    IReadOnlyList<string> MissingRequirements,
    bool CanSubmitForReview,
    IReadOnlyList<DriverHomeChecklistItemDto> Checklist);

public record DriverHomeChecklistItemDto(
    string Code,
    bool Completed,
    string? Note,
    bool Critical);

public record DriverIncomingOfferDto(
    Guid AssignmentId,
    Guid OrderId,
    string OrderNumber,
    string VendorName,
    string PickupAddress,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    string CustomerName,
    string DeliveryAddress,
    decimal? DeliveryLatitude,
    decimal? DeliveryLongitude,
    decimal EstimatedDistanceKm,
    string EstimatedEta,
    decimal Payout,
    string PaymentMethod,
    decimal TotalAmount,
    decimal CodAmount,
    string VendorInitials,
    string CustomerInitials,
    string? PackageNote,
    int CountdownSeconds,
    IReadOnlyList<DriverOfferItemDto> OrderItems);

public record DriverOfferItemDto(
    string Name,
    int Quantity,
    string? Note);

public record DriverCurrentAssignmentDto(
    Guid AssignmentId,
    Guid OrderId,
    string OrderNumber,
    string Status,
    string VendorName,
    string PickupAddress,
    string DeliveryAddress,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    decimal? DeliveryLatitude,
    decimal? DeliveryLongitude,
    decimal CodAmount,
    DateTime CreatedAtUtc,
    string MerchantContact,
    string? VehicleType,
    string? PlateNumber,
    bool PickupOtpRequired,
    bool DeliveryOtpRequired,
    string? PickupOtpCode);

public record DriverEarningsSummaryDto(
    decimal EarningsAmount,
    int CompletedTrips);

public record DriverOfferActionResultDto(
    Guid AssignmentId,
    Guid OrderId,
    string Status,
    string MessageAr,
    string MessageEn);

public record DriverOtpVerificationResultDto(
    Guid AssignmentId,
    Guid OrderId,
    string OtpType,
    string Status,
    string MessageAr,
    string MessageEn,
    DriverAssignmentDetailDto? UpdatedAssignment = null);

public record DeliveryPricingSurgeWindowDto(
    Guid Id,
    string Name,
    TimeSpan StartLocalTime,
    TimeSpan EndLocalTime,
    decimal Multiplier,
    bool IsActive);

public record DeliveryPricingRuleDto(
    Guid Id,
    Guid? DeliveryZoneId,
    string City,
    string Name,
    decimal BaseFee,
    decimal IncludedKm,
    decimal PerKmFee,
    decimal MinFee,
    decimal MaxFee,
    bool IsActive,
    IReadOnlyList<DeliveryPricingSurgeWindowDto> SurgeWindows);

public record AdminDriverKPIsDto(
    int Total,
    int OnlineNow,
    int OnMission,
    int UnderReview,
    int Suspended,
    int LowPerformance);

public record AdminDriverListItemDto(
    Guid Id,
    string DriverDisplayId,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? ImageUrl,
    string City,
    string Status,
    string VerificationStatus,
    int ActiveTasks,
    int CompletedTasks,
    decimal AcceptanceRate,
    decimal WalletBalance,
    string Performance,
    DriverVehicleType? VehicleType,
    DateTime LastSeenAt,
    decimal CommitmentScore,
    int DailyRejections,
    int WeeklyRejections,
    string EnforcementLevel,
    DateTime? LastOfferResponseAtUtc,
    string[] Issues,
    string CollectionPaymentStatus,
    string[]? Alerts);

public record AdminDriversListDto(
    AdminDriverListItemDto[] Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    AdminDriverKPIsDto KPIs);

public record AdminDriverDetailDto(
    // Core
    Guid Id,
    string DriverDisplayId,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Email,
    string? ImageUrl,
    string City,
    string Status,
    string VerificationStatus,
    DriverVehicleType? VehicleType,
    DateTime JoinedAt,
    DateTime LastSeenAt,

    // Overview metrics
    int ActiveTasks,
    int CompletedTasks,
    decimal AcceptanceRate,
    decimal WalletBalance,
    string Performance,
    string[] Issues,
    string CollectionPaymentStatus,
    string[]? Alerts,
    decimal CommitmentScore,
    int DailyRejections,
    int WeeklyRejections,
    string EnforcementLevel,
    DateTime? LastOfferResponseAtUtc,
    string? Address,
    string? LicenseNumber,


    // Review
    DateTime? ReviewedAtUtc,
    string? ReviewNote,
    string? SuspensionReason,
    AdminDriverProfileReadinessDto ProfileReadiness,

    // Documents
    AdminDriverDocumentDto[] Documents,

    // Notes
    AdminDriverNoteDto[] Notes,

    // Incidents
    AdminDriverIncidentDto[] Incidents,

    // Finance summary
    AdminDriverFinanceSummaryDto Finance,

    // Assignment history
    AdminDriverAssignmentDto[] RecentAssignments,

    // Detail sections
    AdminDriverOverviewSectionDto Overview,
    AdminDriverWorkflowSectionDto Workflow,
    AdminDriverOperationsSectionDto Operations,
    AdminDriverPerformanceSectionDto PerformanceDetails,
    AdminDriverSupportSectionDto Support,
    AdminDriverComplianceSectionDto Compliance,
    AdminDriverFinanceSectionDto FinanceDetails,
    AdminDriverVerificationSectionDto Verification);

public record AdminDriverDocumentDto(
    string DocumentType,
    string? ImageUrl,
    string Status,
    string? ExpiryInfo);

public record AdminDriverNoteDto(
    Guid Id,
    string AuthorName,
    string Message,
    DateTime CreatedAtUtc);

public record AdminDriverIncidentDto(
    Guid Id,
    string IncidentType,
    string Severity,
    string Status,
    string? ReviewerName,
    Guid? LinkedOrderId,
    string Summary,
    DateTime CreatedAtUtc);

public record AdminDriverFinanceSummaryDto(
    decimal CurrentBalance,
    decimal PendingBalance,
    decimal TotalEarnings,
    decimal CodCollected,
    int TotalSettlements,
    int TotalPayouts);

public record AdminDriverAssignmentDto(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    string Status,
    DateTime? AcceptedAtUtc,
    DateTime? DeliveredAtUtc,
    DateTime? FailedAtUtc,
    string? FailureReason,
    decimal CodAmount);

public record AdminDriverProfileReadinessDto(
    bool IsProfileComplete,
    int CompletionPercent,
    IReadOnlyList<string> MissingRequirements,
    bool CanSubmitForReview,
    AdminDriverVerificationChecklistItemDto[] Checklist);

public record AdminDriverOverviewSectionDto(
    string? Address,
    string? Region,
    string? City,
    string? LicenseNumber,
    decimal CompletionRate,
    decimal CommitmentScore,
    string CollectionPaymentStatus);

public record AdminDriverWorkflowSectionDto(
    string State,
    string Readiness,
    string[] Blockers,
    string[] Alerts,
    AdminDriverWorkflowActionDto[] Actions,
    AdminDriverLifecycleStageDto[] LifecycleStages);

public record AdminDriverWorkflowActionDto(
    string Id,
    string Tone,
    string TargetTab);

public record AdminDriverLifecycleStageDto(
    string Id,
    string State);

public record AdminDriverOperationTaskDto(
    Guid Id,
    string VendorName,
    string CityLabel,
    string Status,
    DateTime AssignedAtUtc,
    int? DurationMinutes,
    string? DelayLabel,
    decimal CodAmount);

public record AdminDriverOperationsSectionDto(
    string? Region,
    string? City,
    decimal? CurrentLatitude,
    decimal? CurrentLongitude,
    decimal? CurrentAccuracyMeters,
    DateTime? LastLocationAtUtc,
    bool LocationUpdatesBlocked,
    string? LocationBlockReason,
    DateTime? LocationBlockedAtUtc,
    int? ActiveDriversInCity,
    decimal? AvgDeliveryMinutes,
    int? CityCapacityLimit,
    AdminDriverOperationTaskDto[] TaskAssignments);

public record AdminDriverPerformanceMetricDto(
    string Id,
    decimal? NumericValue,
    string DisplayValue,
    string? DeltaValue,
    string Tone);

public record AdminDriverPerformanceBenchmarkDto(
    string Id,
    decimal DriverValue,
    decimal RegionValue,
    decimal FleetValue,
    string Unit,
    string InsightCode);

public record AdminDriverPerformanceInsightGroupDto(
    string Id,
    string Tone,
    string Icon,
    string[] ItemCodes);

public record AdminDriverPerformanceSectionDto(
    decimal CompletionRate,
    decimal AcceptanceRate,
    decimal CommitmentScore,
    int CompletedTasks,
    int RejectedOffers,
    int TimedOutOffers,
    AdminDriverPerformanceMetricDto[] Metrics,
    AdminDriverPerformanceBenchmarkDto[] Benchmarks,
    AdminDriverPerformanceInsightGroupDto[] InsightGroups);

public record AdminDriverSupportTicketDto(
    Guid Id,
    string Subject,
    string Status,
    string Priority,
    string Reviewer,
    DateTime UpdatedAtUtc,
    string? LinkedOrderCode);

public record AdminDriverSupportChatMessageDto(
    string Direction,
    string Message,
    DateTime CreatedAtUtc);

public record AdminDriverSupportFollowUpDto(
    string Code,
    string DueLabel,
    string Tone);

public record AdminDriverSupportSectionDto(
    int OpenNotesCount,
    int TicketsCount,
    int PendingFollowUpsCount,
    int EscalationsCount,
    int UnresolvedCount,
    DateTime? LastUpdateAtUtc,
    string? ReviewerName,
    string? ReviewerRole,
    bool ReviewerOnline,
    AdminDriverSupportTicketDto[] Tickets,
    AdminDriverSupportChatMessageDto[] ChatMessages,
    AdminDriverSupportFollowUpDto[] FollowUps);

public record AdminDriverDocumentHealthDto(
    int Valid,
    int Expiring,
    int Review);

public record AdminDriverComplianceSectionDto(
    int OpenCases,
    int CriticalCases,
    int SafetyAlerts,
    int ExpiredDocuments,
    int Suspensions,
    string RiskLevel,
    AdminDriverDocumentHealthDto DocumentHealth);

public record AdminDriverFinanceEntryDto(
    Guid Id,
    string Reference,
    string Type,
    string Status,
    decimal Amount,
    decimal Fee,
    string Method,
    DateTime CreatedAtUtc);

public record AdminDriverFinanceSectionDto(
    decimal AvailableBalance,
    decimal DueAmount,
    decimal CodCollected,
    decimal PendingDeductions,
    DateTime? NextPayoutDateUtc,
    string? PayoutMethod,
    string StatementPeriod,
    AdminDriverFinanceEntryDto[] Entries);

public record AdminDriverVerificationChecklistItemDto(
    string Code,
    bool Completed,
    string? Note,
    bool Critical);

public record AdminDriverVerificationSectionDto(
    string ApplicationId,
    DateTime SubmittedAtUtc,
    string? Reviewer,
    decimal TrustScore,
    int ProgressPercentage,
    string Recommendation,
    string? RecommendationReason,
    AdminDriverVerificationChecklistItemDto[] Checklist,
    string DecisionNote,
    string InternalNote,
    string[] RejectionReasonOptions);

public record DispatchDecisionDto(
    Guid DriverId,
    string DriverName,
    decimal DistanceKm,
    int ActiveTaskCount,
    decimal ReliabilityScore,
    decimal CommitmentScore,
    bool GpsIsFresh,
    string MatchReason,
    string? CommitmentAdjustmentReason);
