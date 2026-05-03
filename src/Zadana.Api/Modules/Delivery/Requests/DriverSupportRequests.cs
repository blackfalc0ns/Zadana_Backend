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
    [property: JsonPropertyName("type_label_ar")] string TypeLabelAr,
    [property: JsonPropertyName("type_label_en")] string TypeLabelEn,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("status_label_ar")] string StatusLabelAr,
    [property: JsonPropertyName("status_label_en")] string StatusLabelEn,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("priority_label_ar")] string PriorityLabelAr,
    [property: JsonPropertyName("priority_label_en")] string PriorityLabelEn,
    [property: JsonPropertyName("reason_code")] string? ReasonCode,
    [property: JsonPropertyName("reason_label_ar")] string? ReasonLabelAr,
    [property: JsonPropertyName("reason_label_en")] string? ReasonLabelEn,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt);

public sealed record DriverSupportReasonResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("label_ar")] string LabelAr,
    [property: JsonPropertyName("label_en")] string LabelEn,
    [property: JsonPropertyName("requires_note")] bool RequiresNote);

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
    [property: JsonPropertyName("type_label_ar")] string TypeLabelAr,
    [property: JsonPropertyName("type_label_en")] string TypeLabelEn,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("status_label_ar")] string StatusLabelAr,
    [property: JsonPropertyName("status_label_en")] string StatusLabelEn,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("priority_label_ar")] string PriorityLabelAr,
    [property: JsonPropertyName("priority_label_en")] string PriorityLabelEn,
    [property: JsonPropertyName("reason_code")] string? ReasonCode,
    [property: JsonPropertyName("reason_label_ar")] string? ReasonLabelAr,
    [property: JsonPropertyName("reason_label_en")] string? ReasonLabelEn,
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
    [property: JsonPropertyName("type_label_ar")] string TypeLabelAr,
    [property: JsonPropertyName("type_label_en")] string TypeLabelEn,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("status_label_ar")] string StatusLabelAr,
    [property: JsonPropertyName("status_label_en")] string StatusLabelEn,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("priority_label_ar")] string PriorityLabelAr,
    [property: JsonPropertyName("priority_label_en")] string PriorityLabelEn,
    [property: JsonPropertyName("queue")] string Queue,
    [property: JsonPropertyName("queue_label_ar")] string QueueLabelAr,
    [property: JsonPropertyName("queue_label_en")] string QueueLabelEn,
    [property: JsonPropertyName("reason_code")] string? ReasonCode,
    [property: JsonPropertyName("reason_label_ar")] string? ReasonLabelAr,
    [property: JsonPropertyName("reason_label_en")] string? ReasonLabelEn,
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
    [property: JsonPropertyName("action_label_ar")] string ActionLabelAr,
    [property: JsonPropertyName("action_label_en")] string ActionLabelEn,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("title_ar")] string TitleAr,
    [property: JsonPropertyName("title_en")] string TitleEn,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("actor_role")] string ActorRole,
    [property: JsonPropertyName("actor_role_label_ar")] string ActorRoleLabelAr,
    [property: JsonPropertyName("actor_role_label_en")] string ActorRoleLabelEn,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt);
