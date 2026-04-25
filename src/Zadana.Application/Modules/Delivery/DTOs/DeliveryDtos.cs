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
    Guid? PrimaryZoneId,
    string? ZoneName,
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
    DriverCommitmentSummaryDto Commitment);

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
    bool DeliveryOtpRequired);

public record DriverEarningsSummaryDto(
    decimal EarningsAmount,
    int CompletedTrips);

public record DriverOfferActionResultDto(
    Guid AssignmentId,
    Guid OrderId,
    string Status,
    string Message);

public record DriverOtpVerificationResultDto(
    Guid AssignmentId,
    Guid OrderId,
    string OtpType,
    string Status,
    string Message);

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

    // Zone
    string? ZoneName,
    Guid? PrimaryZoneId,

    // Review
    DateTime? ReviewedAtUtc,
    string? ReviewNote,
    string? SuspensionReason,

    // Documents
    AdminDriverDocumentDto[] Documents,

    // Notes
    AdminDriverNoteDto[] Notes,

    // Incidents
    AdminDriverIncidentDto[] Incidents,

    // Finance summary
    AdminDriverFinanceSummaryDto Finance,

    // Assignment history
    AdminDriverAssignmentDto[] RecentAssignments);

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
