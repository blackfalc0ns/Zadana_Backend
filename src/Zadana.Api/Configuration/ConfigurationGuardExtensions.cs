using Microsoft.Extensions.Configuration;

namespace Zadana.Api.Configuration;

public static class ConfigurationGuardExtensions
{
    private static readonly string[] PlaceholderTokens =
    [
        "__SET_",
        "CHANGE_ME",
        "YOUR_"
    ];

    public static string GetRequiredSetting(this IConfiguration configuration, string key)
    {
        var value = configuration[key];

        if (string.IsNullOrWhiteSpace(value) || IsPlaceholder(value))
        {
            throw new InvalidOperationException(
                $"Configuration value '{key}' is missing. Provide it via environment variables, user secrets, or deployment settings.");
        }

        return value;
    }

    public static string GetRequiredConnectionString(this IConfiguration configuration, string name)
    {
        var value = configuration.GetConnectionString(name);

        if (string.IsNullOrWhiteSpace(value) || IsPlaceholder(value))
        {
            throw new InvalidOperationException(
                $"Connection string '{name}' is missing. Provide it via environment variables, user secrets, or deployment settings.");
        }

        return value;
    }

    public static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return PlaceholderTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}
