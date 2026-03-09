using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Infrastructure.Services;

public class ResendOtpService : IOtpService
{
    private readonly IEmailService _emailService;
    private readonly ILogger<ResendOtpService> _logger;

    public ResendOtpService(IEmailService emailService, ILogger<ResendOtpService> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task SendOtpEmailAsync(string emailAddress, string otpCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var subject = "كود التحقق الخاص بك | Your Verification Code";
            var body = $@"
                <div style='font-family: sans-serif; direction: rtl; text-align: center; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                    <h2 style='color: #333;'>زادنا - كود التحقق</h2>
                    <p style='font-size: 18px;'>كود التحقق الخاص بك هو:</p>
                    <div style='font-size: 32px; font-weight: bold; color: #4CAF50; margin: 20px 0; letter-spacing: 5px;'>{otpCode}</div>
                    <p style='color: #666;'>هذا الكود صالح لمدة 5 دقائق فقط.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                    <div style='direction: ltr; text-align: center;'>
                        <h2 style='color: #333;'>Zadana - Verification Code</h2>
                        <p style='font-size: 18px;'>Your verification code is:</p>
                        <div style='font-size: 32px; font-weight: bold; color: #4CAF50; margin: 20px 0; letter-spacing: 5px;'>{otpCode}</div>
                        <p style='color: #666;'>This code is valid for 5 minutes only.</p>
                    </div>
                </div>";

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
