using System.Text.Json.Serialization;

namespace Zadana.Api.Modules.Orders.Requests;

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
    [property: JsonPropertyName("initiator_role")] string InitiatorRole,
    [property: JsonPropertyName("waiting_on_role")] string? WaitingOnRole,
    [property: JsonPropertyName("participants")] List<OrderSupportCaseParticipantResponse> Participants,
    [property: JsonPropertyName("allowed_actions")] List<string> AllowedActions,
    [property: JsonPropertyName("attachments")] List<OrderSupportCaseAttachmentResponse> Attachments,
    [property: JsonPropertyName("activities")] List<OrderSupportCaseActivityResponse> Activities,
    [property: JsonPropertyName("messages")] List<OrderSupportCaseMessageResponse> Messages);

public record OrderSupportCaseAttachmentResponse(
    [property: JsonPropertyName("file_name")] string FileName,
    [property: JsonPropertyName("file_url")] string FileUrl);

public record OrderSupportCaseActivityResponse(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("actor_role")] string ActorRole,
    [property: JsonPropertyName("visible_to_customer")] bool VisibleToCustomer,
    [property: JsonPropertyName("message_type")] string MessageType,
    [property: JsonPropertyName("visible_to")] List<string> VisibleTo,
    [property: JsonPropertyName("is_internal_only")] bool IsInternalOnly,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt);

public record OrderSupportCaseMessageResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("message_type")] string MessageType,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("author_role")] string AuthorRole,
    [property: JsonPropertyName("visible_to")] List<string> VisibleTo,
    [property: JsonPropertyName("is_internal_only")] bool IsInternalOnly,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("attachments")] List<OrderSupportCaseAttachmentResponse> Attachments);

public record OrderSupportCaseParticipantResponse(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("is_initiator")] bool IsInitiator,
    [property: JsonPropertyName("is_awaiting_response")] bool IsAwaitingResponse,
    [property: JsonPropertyName("has_messages")] bool HasMessages);

public record OrderSupportCaseAttachmentUploadResponse(
    [property: JsonPropertyName("file_name")] string FileName,
    [property: JsonPropertyName("url")] string Url);

public record CustomerRefundStatusResponse(
    [property: JsonPropertyName("order_id")] Guid OrderId,
    [property: JsonPropertyName("has_active_case")] bool HasActiveCase,
    [property: JsonPropertyName("case_status")] string? CaseStatus,
    [property: JsonPropertyName("case_type")] string? CaseType,
    [property: JsonPropertyName("requested_amount")] decimal? RequestedAmount,
    [property: JsonPropertyName("approved_amount")] decimal? ApprovedAmount,
    [property: JsonPropertyName("refund_method")] string? RefundMethod,
    [property: JsonPropertyName("refund_status")] string? RefundStatus,
    [property: JsonPropertyName("customer_note")] string? CustomerNote,
    [property: JsonPropertyName("created_at")] DateTime? CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTime? UpdatedAt);

public record CustomerReplyRequest(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("attachments")] List<CreateOrderComplaintAttachmentRequest>? Attachments);

public record CustomerReplyResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("case")] OrderSupportCaseResponse Case);
