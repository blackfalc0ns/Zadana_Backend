using MediatR;

namespace Zadana.Application.Modules.Marketing.Events;

/// <summary>
/// Published when a new banner is created or an existing banner is activated.
/// Triggers a broadcast notification to all connected customers.
/// </summary>
public record BannerActivatedNotification(
    Guid BannerId,
    string TitleAr,
    string TitleEn,
    string ImageUrl) : INotification;
