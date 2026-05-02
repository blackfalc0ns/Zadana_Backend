using System.Text.Json.Serialization;

namespace Zadana.Api.Modules.Delivery.Requests;

public sealed record DriverReportIssueRequest(
    [property: JsonPropertyName("reason_code")] string? ReasonCode,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("attachments")] List<DriverSupportAttachmentInput>? Attachments);

public sealed record DriverDisputeRequest(
    [property: JsonPropertyName("reason_code")] string? ReasonCode,
    [property: JsonPropertyName("message")] string Message);

public sealed record DriverSupportAttachmentInput(
    [property: JsonPropertyName("file_name")] string FileName,
    [property: JsonPropertyName("file_url")] string FileUrl);

public sealed record DriverSupportCaseResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("order_id")] Guid OrderId,
    [property: JsonPropertyName("order_number")] string OrderNumber,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("reason_code")] string? ReasonCode,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt);

public sealed record DriverSupportCasesListResponse(
    [property: JsonPropertyName("items")] List<DriverSupportCaseListItemResponse> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("page_size")] int PageSize,
    [property: JsonPropertyName("total")] int Total);

public sealed record DriverSupportCaseListItemResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("order_id")] Guid OrderId,
    [property: JsonPropertyName("order_number")] string OrderNumber,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("reason_code")] string? ReasonCode,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("admin_note")] string? AdminNote,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTime UpdatedAt,
    [property: JsonPropertyName("closed_at")] DateTime? ClosedAt);

public sealed record DriverSupportCaseDetailResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("order_id")] Guid OrderId,
    [property: JsonPropertyName("order_number")] string OrderNumber,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("queue")] string Queue,
    [property: JsonPropertyName("reason_code")] string? ReasonCode,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("admin_note")] string? AdminNote,
    [property: JsonPropertyName("decision_notes")] string? DecisionNotes,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTime UpdatedAt,
    [property: JsonPropertyName("closed_at")] DateTime? ClosedAt,
    [property: JsonPropertyName("attachments")] List<DriverSupportCaseAttachmentResponse> Attachments,
    [property: JsonPropertyName("activities")] List<DriverSupportCaseActivityResponse> Activities);

public sealed record DriverSupportCaseAttachmentResponse(
    [property: JsonPropertyName("file_name")] string FileName,
    [property: JsonPropertyName("file_url")] string FileUrl);

public sealed record DriverSupportCaseActivityResponse(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("actor_role")] string ActorRole,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt);
