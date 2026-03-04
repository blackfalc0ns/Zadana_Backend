using Microsoft.Extensions.Options;
using Resend;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Infrastructure.Email;

public class ResendEmailSettings
{
    public const string SectionName = "ResendSettings";
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}

public class ResendEmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly ResendEmailSettings _settings;

    public ResendEmailService(IResend resend, IOptions<ResendEmailSettings> settings)
    {
        _resend = resend;
        _settings = settings.Value;
    }

    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage();
        message.From = string.IsNullOrWhiteSpace(_settings.FromName) 
            ? _settings.FromEmail 
            : $"{_settings.FromName} <{_settings.FromEmail}>";
        message.To.Add(to);
        message.Subject = subject;
        message.HtmlBody = body;

        await _resend.EmailSendAsync(message, cancellationToken);
    }
}
