using System.Runtime.CompilerServices;

namespace Zadana.SharedKernel.Guards;

public static class Guard
{
    public static T AgainstNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName);

        return value;
    }

    public static string AgainstNullOrEmpty(string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or empty.", paramName);

        return value;
    }

    public static decimal AgainstNegative(decimal value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(paramName, "Value cannot be negative.");

        return value;
    }

    public static decimal AgainstNonPositive(decimal value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(paramName, "Value must be positive.");

        return value;
    }

    public static int AgainstNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(paramName, "Value cannot be negative.");

        return value;
    }

    public static decimal AgainstOutOfRange(decimal value, decimal min, decimal max, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < min || value > max)
            throw new ArgumentOutOfRangeException(paramName, $"Value must be between {min} and {max}.");

        return value;
    }
}
