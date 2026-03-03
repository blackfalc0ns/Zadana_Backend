namespace Zadana.SharedKernel.Exceptions;

public class ForbiddenAccessException : Exception
{
    public string ErrorCode { get; } = "FORBIDDEN";

    public ForbiddenAccessException(string? message = null)
        : base(message ?? "You do not have permission to perform this action.")
    {
    }
}
