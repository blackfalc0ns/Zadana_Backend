using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using Zadana.Application.Common.Interfaces;
using Zadana.SharedKernel.Exceptions;

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

    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            var from = string.IsNullOrWhiteSpace(_settings.FromName) 
                ? _settings.FromEmail 
                : $"{_settings.FromName} <{_settings.FromEmail}>";

            var requestBody = new
            {
                from = from,
                to = new[] { to },
                subject = subject,
                html = body
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Content = JsonContent.Create(requestBody);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Resend API failed with status {Status}. Error: {Error}", response.StatusCode, errorContent);
                throw new ExternalServiceException("RESEND_API_ERROR", $"Resend email delivery failed. Provider response: {errorContent}");
            }

            _logger.LogInformation("Email sent successfully to {Email}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending email via Resend to {Email}", to);
            throw;
        }
    }
}
