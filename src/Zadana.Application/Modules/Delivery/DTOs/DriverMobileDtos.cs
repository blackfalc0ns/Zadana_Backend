namespace Zadana.Application.Modules.Delivery.DTOs;

public record DriverAssignmentDetailDto(
    Guid AssignmentId,
    Guid OrderId,
    string OrderNumber,
    string AssignmentStatus,
    string HomeState,
    IReadOnlyList<string> AllowedActions,
    string VendorName,
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
    decimal CodAmount,
    bool PickupOtpRequired,
    string PickupOtpStatus,
    bool DeliveryOtpRequired,
    string DeliveryOtpStatus,
    string DriverArrivalState,
    IReadOnlyList<DriverAssignmentItemDto> OrderItems);

public record DriverAssignmentItemDto(
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public record DriverCompletedOrdersListDto(
    IReadOnlyList<DriverCompletedOrderListItemDto> Items,
    int TotalCount);

public record DriverCompletedOrderListItemDto(
    Guid Id,
    string MerchantName,
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
    string? NationalId,
    string? PersonalPhotoUrl,
    string? NationalIdFrontImageUrl,
    string? NationalIdBackImageUrl,
    string? LicenseImageUrl,
    string? VehicleImageUrl,
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
    bool IsProfileComplete,
    int CompletionPercent,
    IReadOnlyList<string> MissingRequirements,
    bool CanSubmitForReview);
