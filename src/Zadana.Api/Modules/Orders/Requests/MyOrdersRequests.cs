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
    [property: JsonPropertyName("items")] List<CustomerOrderProductResponse> Items,
    [property: JsonPropertyName("active_case")] OrderSupportCaseSummaryResponse? ActiveCase);

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
    [property: JsonPropertyName("active_case")] OrderSupportCaseSummaryResponse? ActiveCase,
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

public record CreateOrderSupportCaseRequest(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("reason_code")] string? ReasonCode,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("attachments")] List<CreateOrderComplaintAttachmentRequest>? Attachments);

public record CreateOrderSupportCaseResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("case")] OrderSupportCaseResponse Case);

public record GetOrderSupportCasesResponse(
    [property: JsonPropertyName("items")] List<OrderSupportCaseResponse> Items);

public record GetOrderSupportCaseResponse(
    [property: JsonPropertyName("case")] OrderSupportCaseResponse Case);

public record OrderSupportCaseSummaryResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("queue")] string Queue,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("reason_code")] string? ReasonCode,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTime UpdatedAt);

public record OrderSupportCaseResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("order_id")] Guid OrderId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("queue")] string Queue,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("reason_code")] string? ReasonCode,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("customer_visible_note")] string? CustomerVisibleNote,
    [property: JsonPropertyName("decision_notes")] string? DecisionNotes,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTime UpdatedAt,
    [property: JsonPropertyName("sla_due_at_utc")] DateTime? SlaDueAtUtc,
    [property: JsonPropertyName("requested_refund_amount")] decimal? RequestedRefundAmount,
    [property: JsonPropertyName("approved_refund_amount")] decimal? ApprovedRefundAmount,
    [property: JsonPropertyName("refund_method")] string? RefundMethod,
    [property: JsonPropertyName("cost_bearer")] string? CostBearer,
    [property: JsonPropertyName("attachments")] List<OrderSupportCaseAttachmentResponse> Attachments,
    [property: JsonPropertyName("activities")] List<OrderSupportCaseActivityResponse> Activities);

public record OrderSupportCaseAttachmentResponse(
    [property: JsonPropertyName("file_name")] string FileName,
    [property: JsonPropertyName("file_url")] string FileUrl);

public record OrderSupportCaseActivityResponse(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("actor_role")] string ActorRole,
    [property: JsonPropertyName("visible_to_customer")] bool VisibleToCustomer,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt);

public record OrderSupportCaseAttachmentUploadResponse(
    [property: JsonPropertyName("file_name")] string FileName,
    [property: JsonPropertyName("url")] string Url);
