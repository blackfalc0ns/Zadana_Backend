using System.Text.Json.Serialization;

namespace Zadana.Api.Realtime.Contracts;

public sealed record OrderSupportCaseChangedRealtimePayload(
    [property: JsonPropertyName("caseId")] Guid CaseId,
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("orderNumber")] string OrderNumber,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("targetUrl")] string TargetUrl,
    [property: JsonPropertyName("changedAtUtc")] DateTime ChangedAtUtc);
