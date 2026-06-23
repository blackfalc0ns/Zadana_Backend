namespace Zadana.Api.Modules.Identity.Requests;

public record VerifyOtpRequest(string Identifier, string OtpCode);

public record ResendOtpRequest(string Identifier, string? Purpose = null);

public record RefreshTokenRequest(string RefreshToken);

public record ForgotPasswordRequest(string Identifier);

public record VerifyPasswordResetOtpRequest(string Identifier, string OtpCode);

public record ResetPasswordRequest(string Identifier, string ResetToken, string NewPassword);

public record LogoutRequest(string RefreshToken);

public record ChangeTemporaryPasswordRequest(string CurrentPassword, string NewPassword);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UpdateProfileRequest(string FullName, string Email, string Phone);

public record UpdateProfilePhotoRequest(string ProfilePhotoUrl);
