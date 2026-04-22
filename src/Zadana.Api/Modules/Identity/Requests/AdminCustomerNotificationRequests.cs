namespace Zadana.Api.Modules.Identity.Requests;

public sealed class AdminSendCustomerNotificationRequest
{
    public string? TitleAr { get; init; }
    public string? TitleEn { get; init; }
    public string? BodyAr { get; init; }
    public string? BodyEn { get; init; }
    public string? Type { get; init; }
    public Guid? ReferenceId { get; init; }
    public string? Data { get; init; }
    public string? TargetUrl { get; init; }
    public bool SendPush { get; init; } = true;
}

public record AdminCustomerNotificationResponse(
    string Message,
    Guid CustomerId,
    Guid UserId,
    string ExternalId,
    string Type,
    bool InboxRequested,
    bool PushAttempted,
    bool PushSent,
    bool PushSkipped,
    int? PushStatusCode,
    string? ProviderNotificationId,
    string? PushReason);
