namespace Zadana.Application.Common.Interfaces;

public sealed record SendEmailRequest(
    string[] To,
    string Subject,
    string HtmlBody,
    string? TextBody = null,
    string? From = null,
    string? ReplyTo = null,
    string[]? Cc = null,
    string[]? Bcc = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    IReadOnlyDictionary<string, string>? Headers = null);

public sealed record EmailSendResult(
    string Provider,
    bool Success,
    string? ProviderMessageId = null,
    string? FailureReason = null);

public interface IEmailService
{
    Task<EmailSendResult> SendEmailAsync(SendEmailRequest request, CancellationToken cancellationToken = default);
}
