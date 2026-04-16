using System.Text.Json.Serialization;

namespace Zadana.Api.Modules.Orders.Requests;

public record CustomerOrdersResponse(
    [property: JsonPropertyName("items")] List<CustomerOrderListItemResponse> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("per_page")] int PerPage,
    [property: JsonPropertyName("total")] int Total);

public record CustomerOrderListItemResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("total_price")] decimal TotalPrice,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("items_count")] int ItemsCount,
    [property: JsonPropertyName("items")] List<CustomerOrderProductResponse> Items);

public record CustomerOrderProductResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("price")] decimal Price);

public record CustomerOrderDetailResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("total_price")] decimal TotalPrice,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("can_cancel")] bool CanCancel,
    [property: JsonPropertyName("items_count")] int ItemsCount,
    [property: JsonPropertyName("summary")] CustomerOrderSummaryResponse Summary,
    [property: JsonPropertyName("items")] List<CustomerOrderProductResponse> Items);

public record CustomerOrderSummaryResponse(
    [property: JsonPropertyName("subtotal")] decimal Subtotal,
    [property: JsonPropertyName("shipping_cost")] decimal ShippingCost,
    [property: JsonPropertyName("total")] decimal Total);

public record CancelCustomerOrderRequest(
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("note")] string? Note);

public record CancelCustomerOrderResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("order")] CancelledOrderStatusResponse Order);

public record CancelledOrderStatusResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("status")] string Status);

public record CreateOrderComplaintRequest(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("attachments")] List<CreateOrderComplaintAttachmentRequest>? Attachments);

public record CreateOrderComplaintAttachmentRequest(
    [property: JsonPropertyName("file_name")] string FileName,
    [property: JsonPropertyName("file_url")] string FileUrl);

public record CreateOrderComplaintResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("complaint")] OrderComplaintResponse Complaint);

public record GetOrderComplaintResponse(
    [property: JsonPropertyName("complaint")] OrderComplaintResponse Complaint);

public record OrderComplaintResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("attachments")] List<OrderComplaintAttachmentResponse> Attachments,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt);

public record OrderComplaintAttachmentResponse(
    [property: JsonPropertyName("file_name")] string FileName,
    [property: JsonPropertyName("file_url")] string FileUrl);
