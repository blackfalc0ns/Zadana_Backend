using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zadana.Api.Modules.Identity.Requests;

[JsonConverter(typeof(VerifyOtpRequestJsonConverter))]
public record VerifyOtpRequest(string Identifier, string OtpCode, string? RegistrationToken = null);

public record ResendOtpRequest(string Identifier, string? Purpose = null, string? RegistrationToken = null);

public record RefreshTokenRequest(string RefreshToken);

public record ForgotPasswordRequest(string Identifier);

public record VerifyPasswordResetOtpRequest(string Identifier, string OtpCode);

public record ResetPasswordRequest(string Identifier, string ResetToken, string NewPassword);

public record LogoutRequest(string RefreshToken);

public record ChangeTemporaryPasswordRequest(string CurrentPassword, string NewPassword);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UpdateProfileRequest(string FullName, string Email, string Phone);

public record UpdateProfilePhotoRequest(string ProfilePhotoUrl);

internal sealed class VerifyOtpRequestJsonConverter : JsonConverter<VerifyOtpRequest>
{
    public override VerifyOtpRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var identifier = ReadString(root, "identifier", "email", "phone") ?? string.Empty;
        var otp = ReadString(root, "otpCode", "otp_code", "otp", "code") ?? string.Empty;
        var token = ReadString(root, "registrationToken", "registration_token");
        return new VerifyOtpRequest(
            identifier,
            otp,
            string.IsNullOrWhiteSpace(token) ? null : token);
    }

    public override void Write(Utf8JsonWriter writer, VerifyOtpRequest value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("identifier", value.Identifier);
        writer.WriteString("otpCode", value.OtpCode);
        if (!string.IsNullOrWhiteSpace(value.RegistrationToken))
        {
            writer.WriteString("registrationToken", value.RegistrationToken);
        }

        writer.WriteEndObject();
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetPropertyIgnoreCase(root, name, out var property))
            {
                continue;
            }

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                _ => null
            };
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string name, out JsonElement property)
    {
        foreach (var candidate in root.EnumerateObject())
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}
