using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Application.Modules.Vendors.Interfaces;

public interface IVendorCommunicationService
{
    Task<VendorCommunicationDispatchResult> SendAsync(
        Vendor vendor,
        VendorCommunicationMessage message,
        CancellationToken cancellationToken = default);
}

public sealed record VendorCommunicationMessage(
    string Type,
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string TargetUrl,
    Guid? ReferenceId = null,
    bool SendInbox = true,
    bool SendPush = false,
    bool SendEmail = true,
    string? Data = null);

public sealed record VendorCommunicationDispatchResult(
    bool InboxRequested,
    bool PushAttempted,
    bool PushSent,
    bool PushSkipped,
    int? PushStatusCode,
    string? ProviderNotificationId,
    string? PushReason,
    bool EmailAttempted,
    bool EmailSent,
    bool EmailSkipped,
    string? EmailReason);
