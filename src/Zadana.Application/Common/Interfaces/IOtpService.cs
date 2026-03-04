namespace Zadana.Application.Common.Interfaces;

public interface IOtpService
{
    Task SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default);
    Task SendOtpEmailAsync(string emailAddress, string otpCode, CancellationToken cancellationToken = default);
}
