namespace Zadana.Application.Modules.Orders.DTOs;

public record AdminVendorOrderStatsDto(
    int TotalOrders,
    int OpenOrders,
    int CompletedOrders,
    int CancelledOrders,
    int PaidOrders,
    decimal TotalSalesValue,
    decimal AverageOrderValue);
