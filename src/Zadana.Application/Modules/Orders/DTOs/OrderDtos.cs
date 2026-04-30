namespace Zadana.Application.Modules.Orders.DTOs;

public record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid UserId,
    Guid VendorId,
    Guid CustomerAddressId,
    string Status,
    string PaymentMethod,
    string PaymentStatus,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal TotalAmount,
    DateTime PlacedAtUtc,
    List<OrderItemDto> Items);

public record OrderItemDto(
    Guid Id,
    Guid VendorProductId,
    Guid MasterProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public record AdminVendorOrderListItemDto(
    Guid Id,
    string OrderNumber,
    Guid VendorId,
    Guid CustomerId,
    string CustomerName,
    string Status,
    string PaymentStatus,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal CommissionAmount,
    decimal TotalAmount,
    int ItemsCount,
    DateTime PlacedAtUtc);

public record VendorOrderListItemDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string CustomerPhone,
    string Status,
    string PaymentStatus,
    string PaymentMethod,
    decimal TotalAmount,
    int ItemsCount,
    DateTime PlacedAtUtc,
    bool IsLate);

public record GeoPointDto(decimal Latitude, decimal Longitude);

public record DriverLiveLocationDto(
    decimal Latitude,
    decimal Longitude,
    decimal? AccuracyMeters,
    DateTime RecordedAtUtc);

public record VendorOrderDetailDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string CustomerPhone,
    string CustomerAddress,
    string Status,
    string PaymentStatus,
    string PaymentMethod,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal TotalAmount,
    string? Notes,
    DateTime PlacedAtUtc,
    AssignedDriverSummaryDto? AssignedDriver,
    string DriverArrivalState,
    DateTime? DriverArrivalUpdatedAtUtc,
    string? PickupOtp,
    bool CanConfirmPickup,
    string PickupOtpStatus,
    GeoPointDto? VendorLocation,
    GeoPointDto? CustomerLocation,
    DriverLiveLocationDto? DriverLiveLocation,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<VendorOrderTimelineItemDto> Timeline);

public record AssignedDriverSummaryDto(
    Guid Id,
    string Name,
    string? PhoneNumber,
    string VehicleType,
    string PlateNumber);

public record VendorOrderTimelineItemDto(
    string Status,
    string Label,
    DateTime TimestampUtc,
    bool IsCompleted,
    string? Note);

public record CustomerOrderListDto(
    IReadOnlyList<CustomerOrderListItemDto> Items,
    int Page,
    int PerPage,
    int Total);

public record CustomerOrderListItemDto(
    Guid Id,
    DateTime CreatedAt,
    decimal TotalPrice,
    string Status,
    string PaymentStatus,
    string PaymentMethod,
    bool CanRetryPayment,
    bool CanDelete,
    bool CanCancel,
    int ItemsCount,
    IReadOnlyList<CustomerOrderProductDto> Items);

public record CustomerOrderDetailDto(
    Guid Id,
    DateTime CreatedAt,
    decimal TotalPrice,
    string Status,
    string PaymentStatus,
    string PaymentMethod,
    bool CanRetryPayment,
    bool CanDelete,
    bool CanCancel,
    int ItemsCount,
    CustomerOrderPriceSummaryDto Summary,
    IReadOnlyList<CustomerOrderProductDto> Items,
    OrderSupportCaseSummaryDto? ActiveCase);

public record CustomerOrderPriceSummaryDto(
    decimal Subtotal,
    decimal ShippingCost,
    decimal Total);

public record CustomerOrderProductDto(
    Guid Id,
    string Name,
    int Quantity,
    decimal Price);

public record CustomerOrderTrackingDto(
    CustomerOrderTrackingOrderDto Order,
    CustomerOrderEstimatedDeliveryDto? EstimatedDelivery,
    CustomerOrderTrackingDriverDto? Driver,
    AssignedDriverSummaryDto? AssignedDriver,
    string DriverArrivalState,
    DateTime? DriverArrivalUpdatedAtUtc,
    string? DeliveryOtp,
    bool ShowDeliveryOtp,
    OrderSupportCaseSummaryDto? ActiveCase,
    IReadOnlyList<CustomerOrderTrackingTimelineItemDto> Timeline);

public record CustomerOrderTrackingOrderDto(
    Guid Id,
    string Status);

public record CustomerOrderEstimatedDeliveryDto(
    DateTime Datetime,
    string Formatted);

public record CustomerOrderTrackingDriverDto(
    Guid Id,
    string Name,
    string? PhoneNumber,
    string Subtitle);

public record CustomerOrderTrackingTimelineItemDto(
    string Id,
    string Title,
    string Time,
    bool IsActive,
    bool IsCompleted);

public record OrderComplaintDto(
    Guid Id,
    string Status,
    string Message,
    IReadOnlyList<OrderComplaintAttachmentDto> Attachments,
    DateTime CreatedAt);

public record OrderComplaintAttachmentDto(
    string FileName,
    string FileUrl);

public record OrderSupportCaseSummaryDto(
    Guid Id,
    string Type,
    string Status,
    string Queue,
    string Priority,
    string? ReasonCode,
    string Message,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record OrderSupportCaseDto(
    Guid Id,
    Guid OrderId,
    string Type,
    string Status,
    string Queue,
    string Priority,
    string? ReasonCode,
    string Message,
    string? CustomerVisibleNote,
    string? DecisionNotes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? SlaDueAtUtc,
    decimal? RequestedRefundAmount,
    decimal? ApprovedRefundAmount,
    string? RefundMethod,
    string? CostBearer,
    IReadOnlyList<OrderSupportCaseAttachmentDto> Attachments,
    IReadOnlyList<OrderSupportCaseActivityDto> Activities);

public record OrderSupportCaseAttachmentDto(
    string FileName,
    string FileUrl);

public record OrderSupportCaseActivityDto(
    string Action,
    string Title,
    string? Note,
    string ActorRole,
    bool VisibleToCustomer,
    DateTime CreatedAt);

public record AdminOrderSupportCasesListDto(
    IReadOnlyList<AdminOrderSupportCaseListItemDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);

public record AdminOrderSupportCaseListItemDto(
    Guid Id,
    Guid OrderId,
    string OrderDisplayId,
    string CustomerName,
    string CustomerEmail,
    string MerchantName,
    string Type,
    string Reason,
    decimal Amount,
    string Status,
    string Priority,
    string Owner,
    string Queue,
    string Risk,
    string CreatedAt,
    string Sla,
    string Note,
    string PaymentMask,
    string CustomerSummary,
    string MerchantSummary,
    IReadOnlyList<OrderSupportCaseAttachmentDto> Evidence,
    IReadOnlyList<AdminOrderSupportCaseTimelineItemDto> Timeline);

public record AdminOrderSupportCaseTimelineItemDto(
    string Title,
    string Time,
    string Tone);

public record AdminOrdersListDto(
    IReadOnlyList<AdminOrderListItemDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage,
    AdminOrdersSummaryDto Summary);

public record AdminOrdersSummaryDto(
    int Total,
    int Active,
    int Late,
    int PaymentIssues,
    int Refunds);

public record AdminOrderListItemDto(
    Guid Id,
    string DisplayId,
    string CustomerName,
    string CustomerPhone,
    string MerchantName,
    string MerchantBranch,
    string Date,
    string Time,
    string Status,
    string PaymentStatus,
    string FulfillmentStatus,
    string DispatchState,
    string DispatchReason,
    string PaymentMethodLabel,
    DateTime LastUpdatedAtUtc,
    decimal Total,
    bool IsLate,
    bool HasActiveIssue,
    string? CancellationReason,
    AdminOrderOperationalCaseDto? OperationalCase);

public record AdminOrderDetailDto(
    Guid Id,
    string DisplayId,
    string CustomerName,
    string CustomerPhone,
    string CustomerEmail,
    string CustomerAddress,
    string MerchantName,
    string MerchantBranch,
    string MerchantLocation,
    string? DriverId,
    string DriverName,
    string DriverPhone,
    string DriverVehicleLabel,
    string DriverPlateNumber,
    string City,
    string District,
    int SlaScore,
    string Date,
    string Time,
    string Status,
    string PaymentStatus,
    string FulfillmentStatus,
    string DispatchState,
    string DispatchReason,
    string PaymentMethodLabel,
    string ExpectedDeliveryWindow,
    string TransactionRef,
    string PaymentStatusNote,
    string FulfillmentStatusNote,
    string SupportSummary,
    string AlertLabel,
    DateTime LastUpdatedAtUtc,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Tax,
    decimal Total,
    IReadOnlyList<AdminOrderItemDto> Items,
    IReadOnlyList<AdminOrderTimelineItemDto> Timeline,
    IReadOnlyList<AdminOrderActivityDto> Activities,
    IReadOnlyList<AdminDriverCandidateDto> DriverCandidates,
    IReadOnlyList<string> CandidateScoreBreakdown,
    AdminOrderCancellationSummaryDto? CancellationSummary,
    AdminOrderOperationalCaseDto? OperationalCase);

public record AdminOrderItemDto(
    string Name,
    string Brand,
    string Quantity,
    decimal Price,
    decimal Total,
    string Icon,
    string Sku);

public record AdminOrderTimelineItemDto(
    string Title,
    string Subtitle,
    string Time,
    string Status,
    bool Current);

public record AdminOrderActivityDto(
    string Title,
    string Actor,
    string Time,
    string Tone);

public record AdminDriverCandidateDto(
    string Id,
    string Name,
    string Code,
    string Phone,
    string City,
    string Area,
    string Status,
    decimal DistanceKm,
    int ActiveOrders,
    decimal Rating,
    decimal RejectionRate,
    string LastActivity,
    string Initials,
    string AvatarTone,
    bool LowPerformance,
    bool Verified,
    string DispatchMatchReason,
    decimal CommitmentScore,
    string? CommitmentAdjustmentReason,
    bool GpsFresh,
    bool LowConfidenceGps,
    string DistanceBucket);

public record AdminOrderCancellationSummaryDto(
    string ReasonLabel,
    string Details,
    string RefundType,
    string CostBearer,
    string CancelledAt,
    string CancelledBy,
    string CustomerMessage);

public record AdminOrderOperationalCaseDto(
    string Type,
    string Status,
    string Title,
    string QueueLabel,
    string OpenedAt,
    string LastUpdatedAt);
