namespace Zadana.Api.Modules.Social.Requests;

public sealed class SendVendorTestNotificationRequest
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
