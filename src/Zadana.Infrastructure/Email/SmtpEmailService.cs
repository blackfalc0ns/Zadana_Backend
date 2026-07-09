using System.ComponentModel.DataAnnotations;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Infrastructure.Email;

public sealed class EmailSettings
{
    public const string SectionName = "Email";

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
    [Url]
    public string? OtpHeroImageUrlAr { get; set; }
    [Url]
    public string? OtpHeroImageUrlEn { get; set; }
    [Url]
    public string? PasswordResetHeroImageUrlAr { get; set; }
    [Url]
    public string? PasswordResetHeroImageUrlEn { get; set; }
    public SmtpEmailSettings Smtp { get; set; } = new();
}

public sealed class SmtpEmailSettings
{
    [Required]
    public string Host { get; set; } = string.Empty;
    [Range(1, 65535)]
    public int Port { get; set; } = 587;
    public string Security { get; set; } = "StartTls";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    [Range(5, 120)]
    public int TimeoutSeconds { get; set; } = 30;
    public bool RequireAuthentication { get; set; } = true;
}

/// <summary>
/// Sends email through the configured SMTP server. One authenticated
/// connection is reused because SMTP connect/authenticate is relatively
/// expensive. MailKit's SmtpClient is not thread-safe, so sends are serialized.
/// </summary>
public sealed class SmtpEmailService : IEmailService, IAsyncDisposable
{
    private static readonly HashSet<string> BlockedCustomHeaders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Bcc", "Cc", "Content-Type", "Date", "From", "Message-Id",
            "MIME-Version", "Reply-To", "Subject", "To"
        };

    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly SemaphoreSlim _smtpGate = new(1, 1);
    private readonly SmtpClient _client = new();

    public SmtpEmailService(
        IOptions<EmailSettings> settings,
        ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _client.Timeout = checked(_settings.Smtp.TimeoutSeconds * 1000);
    }

    public async Task<EmailSendResult> SendEmailAsync(
        SendEmailRequest emailRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = BuildMessage(emailRequest);
            await _smtpGate.WaitAsync(cancellationToken);
            try
            {
                await SendWithReconnectRetryAsync(message, cancellationToken);

                _logger.LogInformation(
                    "Email sent through SMTP to {RecipientCount} recipient(s). MessageId={MessageId}",
                    emailRequest.To.Length,
                    message.MessageId);

                return new EmailSendResult("smtp", true, message.MessageId, null);
            }
            catch
            {
                await DisconnectQuietlyAsync();
                throw;
            }
            finally
            {
                _smtpGate.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "SMTP email delivery failed for {RecipientCount} recipient(s).",
                emailRequest.To.Length);
            return new EmailSendResult("smtp", false, null, SanitizeFailureReason(exception));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _smtpGate.WaitAsync();
        try
        {
            await DisconnectQuietlyAsync();
            _client.Dispose();
        }
        finally
        {
            _smtpGate.Release();
            _smtpGate.Dispose();
        }
    }

    internal MimeMessage BuildMessage(SendEmailRequest request)
    {
        if (request.To is not { Length: > 0 })
        {
            throw new ArgumentException("At least one email recipient is required.", nameof(request));
        }

        var message = new MimeMessage
        {
            MessageId = MimeUtils.GenerateMessageId(),
            Subject = request.Subject ?? string.Empty
        };

        var requestedFrom = ParseMailbox(request.From);
        var useRequestedSender = IsAllowedFromEmail(requestedFrom?.Address);
        var fromEmail = useRequestedSender ? requestedFrom!.Address : _settings.FromEmail;
        var fromName = useRequestedSender && !string.IsNullOrWhiteSpace(requestedFrom!.Name)
            ? requestedFrom.Name
            : _settings.FromName;

        message.From.Add(new MailboxAddress(fromName, fromEmail));
        AddAddresses(message.To, request.To);
        AddAddresses(message.Cc, request.Cc);
        AddAddresses(message.Bcc, request.Bcc);

        var replyTo = ParseMailbox(request.ReplyTo);
        if (replyTo is not null)
        {
            message.ReplyTo.Add(replyTo);
        }

        var builder = new BodyBuilder
        {
            HtmlBody = request.HtmlBody,
            TextBody = string.IsNullOrWhiteSpace(request.TextBody)
                ? StripHtmlFallback(request.HtmlBody)
                : request.TextBody
        };
        message.Body = builder.ToMessageBody();

        AddMetadataHeaders(message, request.Metadata);
        AddCustomHeaders(message, request.Headers);
        return message;
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected && _client.IsAuthenticated)
        {
            return;
        }

        if (_client.IsConnected)
        {
            await _client.DisconnectAsync(quit: false, cancellationToken);
        }

        await _client.ConnectAsync(
            _settings.Smtp.Host,
            _settings.Smtp.Port,
            ResolveSocketOptions(_settings.Smtp.Security),
            cancellationToken);

        if (_settings.Smtp.RequireAuthentication)
        {
            await _client.AuthenticateAsync(
                _settings.Smtp.Username,
                _settings.Smtp.Password,
                cancellationToken);
        }
    }

    private static SecureSocketOptions ResolveSocketOptions(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "auto" => SecureSocketOptions.Auto,
            "none" => SecureSocketOptions.None,
            "sslonconnect" or "ssl" => SecureSocketOptions.SslOnConnect,
            "starttlswhenavailable" => SecureSocketOptions.StartTlsWhenAvailable,
            "starttls" or "" => SecureSocketOptions.StartTls,
            _ => throw new InvalidOperationException(
                "Email:Smtp:Security must be Auto, None, SslOnConnect, StartTls, or StartTlsWhenAvailable.")
        };

    private bool IsAllowedFromEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return new[]
            {
                _settings.FromEmail,
                _settings.SupportEmail,
                _settings.HelloEmail,
                _settings.InfoEmail,
                _settings.ContactEmail
            }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Contains(email.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static MailboxAddress? ParseMailbox(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return MailboxAddress.TryParse(value.Trim(), out var address) ? address : null;
    }

    private static void AddAddresses(InternetAddressList target, IEnumerable<string>? addresses)
    {
        if (addresses is null)
        {
            return;
        }

        foreach (var value in addresses.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (!MailboxAddress.TryParse(value.Trim(), out var address))
            {
                throw new FormatException($"Invalid email address: {value}");
            }

            target.Add(address);
        }
    }

    private static void AddMetadataHeaders(
        MimeMessage message,
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null)
        {
            return;
        }

        foreach (var item in metadata)
        {
            TryAddHeader(message, $"X-Zadana-{item.Key}", item.Value);
        }
    }

    private static void AddCustomHeaders(
        MimeMessage message,
        IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return;
        }

        foreach (var item in headers)
        {
            if (!BlockedCustomHeaders.Contains(item.Key))
            {
                TryAddHeader(message, item.Key, item.Value);
            }
        }
    }

    private async Task SendWithReconnectRetryAsync(
        MimeMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureConnectedAsync(cancellationToken);
            await _client.SendAsync(message, cancellationToken);
        }
        catch (Exception exception) when (IsTransientConnectionFailure(exception))
        {
            _logger.LogWarning(
                exception,
                "SMTP connection failed while sending message {MessageId}. Reconnecting and retrying once.",
                message.MessageId);

            await DisconnectQuietlyAsync();
            await EnsureConnectedAsync(cancellationToken);
            await _client.SendAsync(message, cancellationToken);
        }
    }

    private static void TryAddHeader(MimeMessage message, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(value) ||
            name.Contains('\r') ||
            name.Contains('\n') ||
            value.Contains('\r') ||
            value.Contains('\n'))
        {
            return;
        }

        message.Headers.Replace(name.Trim(), value.Trim());
    }

    private static bool IsTransientConnectionFailure(Exception exception) =>
        exception switch
        {
            SmtpCommandException smtp when
                smtp.StatusCode == SmtpStatusCode.ServiceNotAvailable ||
                smtp.Message.Contains("idle timeout", StringComparison.OrdinalIgnoreCase) => true,
            SmtpProtocolException => true,
            IOException => true,
            TimeoutException => true,
            _ => false
        };

    private static string StripHtmlFallback(string html) =>
        string.IsNullOrWhiteSpace(html)
            ? string.Empty
            : System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");

    private static string SanitizeFailureReason(Exception exception) =>
        exception switch
        {
            AuthenticationException => "SMTP authentication failed.",
            SslHandshakeException => "SMTP TLS handshake failed.",
            SmtpCommandException smtp => $"SMTP server rejected the message ({smtp.StatusCode}).",
            _ => "SMTP delivery failed."
        };

    private async Task DisconnectQuietlyAsync()
    {
        if (!_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.DisconnectAsync(quit: true);
        }
        catch
        {
            // The connection is already unusable; disposal/reconnect handles it.
        }
    }
}
