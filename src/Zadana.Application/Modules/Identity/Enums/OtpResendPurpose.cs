namespace Zadana.Application.Modules.Identity.Enums;

public enum OtpResendPurpose
{
    Registration = 0,
    PasswordReset = 1
}

public static class OtpResendPurposeParser
{
    public static OtpResendPurpose Parse(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "password_reset" or "passwordreset" or "reset_password" or "forgot_password" => OtpResendPurpose.PasswordReset,
            "registration" or "register" or "account_verification" or "verify_account" => OtpResendPurpose.Registration,
            _ => OtpResendPurpose.Registration
        };
}
