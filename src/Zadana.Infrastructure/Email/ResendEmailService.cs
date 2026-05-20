using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Mail;
using System.Net.Http.Json;
using System.Text.Json;
using Zadana.Application.Common.Interfaces;
namespace Zadana.Infrastructure.Email;

public class ResendEmailSettings
{
    public const string SectionName = "ResendSettings";
    [Required]
    public string ApiKey { get; set; } = string.Empty;
    [Required]
    [EmailAddress]
    public string FromEmail { get; set; } = string.Empty;
    [Required]
    public string FromName { get; set; } = string.Empty;
    [Required]
    [EmailAddress]
    public string SupportEmail { get; set; } = string.Empty;
    [Required]
    [EmailAddress]
    public string HelloEmail { get; set; } = string.Empty;
    [Required]
    [EmailAddress]
    public string InfoEmail { get; set; } = string.Empty;
    [Required]
    [EmailAddress]
    public string ContactEmail { get; set; } = string.Empty;
    [Required]
    [Url]
    public string LogoUrl { get; set; } = string.Empty;
    [Required]
    [Url]
    public string OtpHeroImageUrl { get; set; } = string.Empty;
}

public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ResendEmailSettings _settings;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(HttpClient httpClient, IOptions<ResendEmailSettings> settings, ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendEmailAsync(SendEmailRequest emailRequest, CancellationToken cancellationToken = default)
    {
        try
        {
            var fromName = ExtractDisplayName(emailRequest.From) ?? _settings.FromName;
            var requestedFromEmail = ExtractEmailAddress(emailRequest.From);
            var fromEmail = IsAllowedFromEmail(requestedFromEmail)
                ? requestedFromEmail!
                : _settings.FromEmail;

            var from = string.IsNullOrWhiteSpace(fromName)
                ? fromEmail
                : $"{fromName} <{fromEmail}>";

            var requestBody = new
            {
                from = from,
                to = emailRequest.To,
                cc = emailRequest.Cc,
                bcc = emailRequest.Bcc,
                reply_to = string.IsNullOrWhiteSpace(emailRequest.ReplyTo) ? null : emailRequest.ReplyTo.Trim(),
                subject = emailRequest.Subject,
                html = emailRequest.HtmlBody,
                text = string.IsNullOrWhiteSpace(emailRequest.TextBody) ? null : emailRequest.TextBody,
                headers = emailRequest.Metadata is { Count: > 0 }
                    ? emailRequest.Metadata.ToDictionary(
                        item => $"X-Zadana-{item.Key}",
                        item => item.Value,
                        StringComparer.OrdinalIgnoreCase)
                    : null
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            httpRequest.Content = JsonContent.Create(requestBody);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Resend API failed with status {Status}. Error: {Error}", response.StatusCode, errorContent);
                return new EmailSendResult("resend", false, null, errorContent);
            }

            string? providerMessageId = null;
            await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
            {
                var payload = await JsonSerializer.DeserializeAsync<ResendEmailResponse>(stream, cancellationToken: cancellationToken);
                providerMessageId = payload?.Id;
            }

            _logger.LogInformation("Email sent successfully to {Recipients}", string.Join(", ", emailRequest.To));
            return new EmailSendResult("resend", true, providerMessageId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending email via Resend to {Recipients}", string.Join(", ", emailRequest.To));
            return new EmailSendResult("resend", false, null, ex.Message);
        }
    }

    private static string? ExtractDisplayName(string? from)
    {
        if (string.IsNullOrWhiteSpace(from))
        {
            return null;
        }

        var normalized = from.Trim();
        var markerIndex = normalized.IndexOf('<');
        return markerIndex > 0 ? normalized[..markerIndex].Trim().Trim('"') : null;
    }

    private static string? ExtractEmailAddress(string? from)
    {
        if (string.IsNullOrWhiteSpace(from))
        {
            return null;
        }

        try
        {
            return new MailAddress(from.Trim()).Address.Trim().ToLowerInvariant();
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private bool IsAllowedFromEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var allowed = new[]
        {
            _settings.FromEmail,
            _settings.SupportEmail,
            _settings.HelloEmail,
            _settings.InfoEmail,
            _settings.ContactEmail
        };

        return allowed
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Contains(email.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ResendEmailResponse
    {
        public string? Id { get; set; }
    }
}
