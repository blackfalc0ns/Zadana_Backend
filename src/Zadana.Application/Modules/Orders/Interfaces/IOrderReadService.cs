using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Orders.DTOs;

namespace Zadana.Application.Modules.Orders.Interfaces;

public interface IOrderReadService
{
    Task<OrderDto?> GetByIdAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
    Task<CustomerOrderListDto> GetCustomerOrdersAsync(
        Guid userId,
        CustomerOrderBucket bucket,
        int page,
        int perPage,
        CancellationToken cancellationToken = default);
    Task<CustomerOrderDetailDto?> GetCustomerOrderDetailAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<CustomerOrderTrackingDto?> GetCustomerOrderTrackingAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<OrderComplaintDto?> GetCustomerOrderComplaintAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<PaginatedList<AdminVendorOrderListItemDto>> GetVendorOrdersAsync(
        Guid vendorId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<PaginatedList<VendorOrderListItemDto>> GetVendorWorkspaceOrdersAsync(
        Guid vendorId,
        string? search,
        string? status,
        string? paymentMethod,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<VendorOrderDetailDto?> GetVendorOrderDetailAsync(
        Guid vendorId,
        Guid orderId,
        CancellationToken cancellationToken = default);
    Task<AdminOrdersListDto> GetAdminOrdersAsync(
        string? search,
        string? status,
        string? paymentStatus,
        string? fulfillmentStatus,
        string? queueView,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<AdminOrderDetailDto?> GetAdminOrderDetailAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}

public enum CustomerOrderBucket
{
    Active,
    Completed,
    Returns
}
