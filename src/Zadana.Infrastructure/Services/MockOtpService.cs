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

    public Task SendOtpEmailAsync(
        string emailAddress,
        string otpCode,
        CancellationToken cancellationToken = default,
        int validityMinutes = 5)
    {
        // Mock provider: never log the actual OTP code. We only emit a
        // fingerprint so devs can correlate without exposing live codes that
        // could be replayed against a real account.
        _logger.LogInformation(
            "[MOCK_EMAIL_OTP] To={Email} CodeLength={Length} CodePreview={Preview} ValidityMinutes={Validity}",
            MaskEmail(emailAddress),
            otpCode?.Length ?? 0,
            MaskOtp(otpCode),
            validityMinutes);

        return Task.CompletedTask;
    }

    public Task SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[MOCK_SMS_OTP] To={Phone} CodeLength={Length} CodePreview={Preview}",
            MaskPhone(phoneNumber),
            otpCode?.Length ?? 0,
            MaskOtp(otpCode));

        return Task.CompletedTask;
    }

    private static string MaskOtp(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return "***";
        }

        // Reveal only the last digit so QA can spot-correlate without exposing
        // the full code. Keeps logs useful but useless to an attacker.
        return code.Length == 1
            ? "*"
            : new string('*', code.Length - 1) + code[^1];
    }

    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "***";
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 1)
        {
            return "***" + (atIndex >= 0 ? email[atIndex..] : string.Empty);
        }

        return string.Concat(email.AsSpan(0, 1), "***", email.AsSpan(atIndex));
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return "***";
        }

        if (phone.Length <= 4)
        {
            return new string('*', phone.Length);
        }

        return string.Concat(new string('*', phone.Length - 4), phone.AsSpan(phone.Length - 4));
    }
}
