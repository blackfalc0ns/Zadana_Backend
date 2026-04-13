namespace Zadana.Api.Modules.Identity.Requests;

public record VerifyOtpRequest(string Identifier, string OtpCode);

public record ResendOtpRequest(string Identifier);

public record RefreshTokenRequest(string RefreshToken);

public record ForgotPasswordRequest(string Identifier);

public record ResetPasswordRequest(string Identifier, string OtpCode, string NewPassword);

public record LogoutRequest(string RefreshToken);

public record UpdateProfileRequest(string FullName, string Email, string Phone);
