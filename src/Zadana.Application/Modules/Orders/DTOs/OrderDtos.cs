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
    int ItemsCount,
    IReadOnlyList<CustomerOrderProductDto> Items);

public record CustomerOrderDetailDto(
    Guid Id,
    DateTime CreatedAt,
    decimal TotalPrice,
    string Status,
    bool CanCancel,
    int ItemsCount,
    CustomerOrderPriceSummaryDto Summary,
    IReadOnlyList<CustomerOrderProductDto> Items);

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
