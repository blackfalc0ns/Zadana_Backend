namespace Zadana.SharedKernel.Exceptions;

public class ForbiddenAccessException : Exception
{
    public string ErrorCode { get; }

    public ForbiddenAccessException(string? message = null, string? errorCode = null)
        : base(message ?? "FORBIDDEN")
    {
        ErrorCode = !string.IsNullOrWhiteSpace(errorCode)
            ? errorCode
            : LooksLikeResourceKey(message) ? message! : "FORBIDDEN";
    }

    private static bool LooksLikeResourceKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsUpper(ch) && !char.IsDigit(ch) && ch != '_')
            {
                return false;
            }
        }

        return true;
    }
}
