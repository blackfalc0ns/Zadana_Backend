namespace Zadana.SharedKernel.Exceptions;

public class UnauthorizedException : Exception
{
    public string? ErrorCode { get; }

    public UnauthorizedException() : base() { }

    public UnauthorizedException(string message, string? errorCode = null) : base(message)
    {
        ErrorCode = !string.IsNullOrWhiteSpace(errorCode)
            ? errorCode
            : LooksLikeResourceKey(message) ? message : null;
    }

    public UnauthorizedException(string message, Exception innerException) : base(message, innerException) { }

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
