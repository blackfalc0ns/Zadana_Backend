namespace Zadana.Application.Common.Interfaces;

public sealed record NotificationDispatchRequest(
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string? Type = null,
    string? Category = null,
    string? Priority = null,
    Guid? ReferenceId = null,
    string? Data = null);
