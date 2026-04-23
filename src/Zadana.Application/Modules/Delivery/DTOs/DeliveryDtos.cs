namespace Zadana.Application.Modules.Delivery.DTOs;

public record DeliveryZoneDto(
    Guid Id,
    string City,
    string Name,
    decimal CenterLat,
    decimal CenterLng,
    decimal RadiusKm,
    bool IsActive);

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
    string? VehicleType,
    DateTime LastSeenAt,
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
    string? VehicleType,
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
    bool GpsIsFresh,
    string MatchReason);
