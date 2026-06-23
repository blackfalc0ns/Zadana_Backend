namespace Zadana.Application.Modules.Delivery.DTOs;

public record DriverAssignmentDetailDto(
    Guid AssignmentId,
    Guid OrderId,
    string OrderNumber,
    string AssignmentStatus,
    string AssignmentStatusLabel,
    string HomeState,
    string HomeStateLabel,
    IReadOnlyList<string> AllowedActions,
    string VendorName,
    string? VendorImageUrl,
    string PickupAddress,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    string StorePhone,
    string CustomerName,
    string DeliveryAddress,
    decimal? DeliveryLatitude,
    decimal? DeliveryLongitude,
    string? CustomerPhone,
    string PaymentMethod,
    string PaymentMethodLabel,
    decimal CodAmount,
    bool PickupOtpRequired,
    string PickupOtpStatus,
    string PickupOtpStatusLabel,
    bool DeliveryOtpRequired,
    string DeliveryOtpStatus,
    string DeliveryOtpStatusLabel,
    string? PickupOtpCode,
    string DriverArrivalState,
    string DriverArrivalStateLabel,
    IReadOnlyList<DriverAssignmentItemDto> OrderItems);

public record DriverAssignmentItemDto(
    string Name,
    string? ImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? DisplaySize,
    string? Unit,
    string? StoreName);

public record DriverCompletedOrdersListDto(
    IReadOnlyList<DriverCompletedOrderListItemDto> Items,
    int TotalCount,
    int Page,
    int PerPage,
    bool HasMore);

public record DriverCompletedOrderListItemDto(
    Guid Id,
    string MerchantName,
    string? MerchantImageUrl,
    string CustomerName,
    DateTime? CompletedAtUtc,
    string Status,
    decimal Amount,
    decimal DistanceKm,
    string PaymentMethod,
    string DeliveryAddress,
    IReadOnlyList<DriverCompletedOrderItemDto> Items);

public record DriverCompletedOrderDetailDto(
    Guid Id,
    Guid AssignmentId,
    string OrderNumber,
    string MerchantName,
    string? MerchantImageUrl,
    string MerchantPhone,
    string CustomerName,
    string? CustomerPhone,
    string PickupAddress,
    string DeliveryAddress,
    string Status,
    string PaymentMethod,
    decimal Amount,
    decimal DeliveryFee,
    decimal DistanceKm,
    DateTime? CompletedAtUtc,
    IReadOnlyList<DriverCompletedOrderItemDto> Items);

public record DriverCompletedOrderItemDto(
    string Name,
    string? ImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public record DriverProfileDto(
    string FullName,
    string Email,
    string Phone,
    string? Address,
    string? VehicleType,
    string? LicenseNumber,
    DateTime? NationalIdExpiryDate,
    DateTime? DriverLicenseExpiryDate,
    string? VehicleLicenseNumber,
    DateTime? VehicleLicenseExpiryDate,
    string? NationalId,
    string? PersonalPhotoUrl,
    string? NationalIdFrontImageUrl,
    string? NationalIdBackImageUrl,
    string? LicenseImageUrl,
    string? VehicleImageUrl,
    IReadOnlyList<DriverProfileDocumentDto> Documents,
    IReadOnlyList<DriverProfileSectionDto> Sections,
    string? Region,
    string? City,
    string? RegionNameAr,
    string? RegionNameEn,
    string? CityNameAr,
    string? CityNameEn,
    string VerificationStatus,
    string AccountStatus,
    string? ReviewNote,
    string? SuspensionReason,
    DriverRejectionPolicyDto RejectionPolicy,
    bool IsProfileComplete,
    int CompletionPercent,
    IReadOnlyList<string> MissingRequirements,
    bool CanSubmitForReview,
    string? ReviewNoteAr = null,
    string? ReviewNoteEn = null);

public record DriverRejectionPolicyDto(
    int DailyRejections,
    int DailyLimit,
    int RemainingBeforeFreeze,
    bool IsFrozen,
    string? RestrictionMessage,
    int WeeklyRejections = 0,
    int WeeklyLimit = 0,
    int RemainingBeforeWeeklyFreeze = 0,
    string? RestrictionMessageAr = null,
    string? RestrictionMessageEn = null);

public record DriverProfileDocumentDto(
    string DocumentType,
    string Status,
    string? RejectionReason,
    DateTime? ReviewedAtUtc,
    string? ReviewedByName);

public record DriverProfileSectionDto(
    string Section,
    string Status,
    string? RejectionReason = null,
    DateTime? ReviewedAtUtc = null);
