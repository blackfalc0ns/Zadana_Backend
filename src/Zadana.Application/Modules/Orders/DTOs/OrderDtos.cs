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
