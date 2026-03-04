using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Infrastructure.Services;

public class MockOtpService : IOtpService
{
    private readonly ILogger<MockOtpService> _logger;

    public MockOtpService(ILogger<MockOtpService> logger)
    {
        _logger = logger;
    }

    public Task SendOtpEmailAsync(string emailAddress, string otpCode, CancellationToken cancellationToken = default)
    {
        // In a real app, integrate SendGrid, AWS SES, SMTP, etc.
        _logger.LogInformation("=========================================");
        _logger.LogInformation("📧 MOCK EMAIL PROVIDER");
        _logger.LogInformation("To: {Email}", emailAddress);
        _logger.LogInformation("Your Zadana Verification Code is: {Code}", otpCode);
        _logger.LogInformation("=========================================");
        
        return Task.CompletedTask;
    }

    public Task SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        // In a real app, integrate Twilio, Unifonic, etc.
        _logger.LogInformation("=========================================");
        _logger.LogInformation("📱 MOCK SMS PROVIDER");
        _logger.LogInformation("To: {Phone}", phoneNumber);
        _logger.LogInformation("Your Zadana Verification Code is: {Code}", otpCode);
        _logger.LogInformation("=========================================");
        
        return Task.CompletedTask;
    }
}
