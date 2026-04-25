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
    [property: JsonPropertyName("payment_status")] string PaymentStatus,
    [property: JsonPropertyName("payment_method")] string PaymentMethod,
    [property: JsonPropertyName("can_retry_payment")] bool CanRetryPayment,
    [property: JsonPropertyName("can_delete")] bool CanDelete,
    [property: JsonPropertyName("can_cancel")] bool CanCancel,
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
    [property: JsonPropertyName("payment_status")] string PaymentStatus,
    [property: JsonPropertyName("payment_method")] string PaymentMethod,
    [property: JsonPropertyName("can_retry_payment")] bool CanRetryPayment,
    [property: JsonPropertyName("can_delete")] bool CanDelete,
    [property: JsonPropertyName("can_cancel")] bool CanCancel,
    [property: JsonPropertyName("items_count")] int ItemsCount,
    [property: JsonPropertyName("summary")] CustomerOrderSummaryResponse Summary,
    [property: JsonPropertyName("items")] List<CustomerOrderProductResponse> Items);

public record RetryOrderPaymentResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("payment")] CheckoutOrderPaymentResponse Payment);

public record CustomerOrderTrackingResponse(
    [property: JsonPropertyName("order")] CustomerOrderTrackingOrderResponse Order,
    [property: JsonPropertyName("estimated_delivery")] CustomerOrderEstimatedDeliveryResponse? EstimatedDelivery,
    [property: JsonPropertyName("driver")] CustomerOrderTrackingDriverResponse? Driver,
    [property: JsonPropertyName("assigned_driver")] CustomerAssignedDriverResponse? AssignedDriver,
    [property: JsonPropertyName("driver_arrival_state")] string DriverArrivalState,
    [property: JsonPropertyName("driver_arrival_updated_at_utc")] DateTime? DriverArrivalUpdatedAtUtc,
    [property: JsonPropertyName("delivery_otp")] string? DeliveryOtp,
    [property: JsonPropertyName("show_delivery_otp")] bool ShowDeliveryOtp,
    [property: JsonPropertyName("timeline")] List<CustomerOrderTrackingTimelineItemResponse> Timeline);

public record CustomerOrderTrackingOrderResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("status")] string Status);

public record CustomerOrderEstimatedDeliveryResponse(
    [property: JsonPropertyName("datetime")] DateTime Datetime,
    [property: JsonPropertyName("formatted")] string Formatted);

public record CustomerOrderTrackingDriverResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("phone_number")] string? PhoneNumber,
    [property: JsonPropertyName("subtitle")] string Subtitle);

public record CustomerAssignedDriverResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("phone_number")] string? PhoneNumber,
    [property: JsonPropertyName("vehicle_type")] string VehicleType,
    [property: JsonPropertyName("plate_number")] string PlateNumber);

public record CustomerOrderTrackingTimelineItemResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("time")] string Time,
    [property: JsonPropertyName("is_active")] bool IsActive,
    [property: JsonPropertyName("is_completed")] bool IsCompleted);

public record CustomerOrderSummaryResponse(
    [property: JsonPropertyName("subtotal")] decimal Subtotal,
    [property: JsonPropertyName("shipping_cost")] decimal ShippingCost,
    [property: JsonPropertyName("total")] decimal Total);

public record CancelCustomerOrderRequest(
    [property: JsonPropertyName("reason_code")] string? ReasonCode,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("note")] string? Note);

public record CancelCustomerOrderResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("order")] CancelledOrderStatusResponse Order);

public record CustomerOrderCancellationReasonResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("label_ar")] string LabelAr,
    [property: JsonPropertyName("label_en")] string LabelEn,
    [property: JsonPropertyName("requires_note")] bool RequiresNote);

public record CancelledOrderStatusResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("status")] string Status);

public record DeleteCustomerOrderResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("order_id")] Guid OrderId,
    [property: JsonPropertyName("deleted")] bool Deleted);

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
