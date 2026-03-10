using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Infrastructure.Services;

public class ResendOtpService : IOtpService
{
    private readonly IEmailService _emailService;
    private readonly ILogger<ResendOtpService> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ITemplateService _templateService;

    public ResendOtpService(
        IEmailService emailService, 
        ILogger<ResendOtpService> logger,
        IStringLocalizer<SharedResource> localizer,
        ITemplateService templateService)
    {
        _emailService = emailService;
        _logger = logger;
        _localizer = localizer;
        _templateService = templateService;
    }

    public async Task SendOtpEmailAsync(string emailAddress, string otpCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var subject = _localizer["OtpEmailSubject"].Value;
            var placeholders = new Dictionary<string, string>
            {
                { "OtpCode", otpCode },
                { "Year", DateTime.UtcNow.Year.ToString() }
            };
            var body = await _templateService.RenderTemplateAsync("OtpEmail", placeholders);

            await _emailService.SendEmailAsync(emailAddress, subject, body, cancellationToken);
            _logger.LogInformation("Email OTP sent successfully to {Email}", emailAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email OTP to {Email}", emailAddress);
            throw;
        }
    }

    public Task SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("SMS OTP is requested but not implemented in ResendOtpService. Use TwilioOtpService if SMS is needed.");
        return Task.CompletedTask;
    }
}
