namespace Zadana.SharedKernel.Exceptions;

public class UnauthorizedException : Exception
{
    public string? ErrorCode { get; }

    public UnauthorizedException() : base() { }

    public UnauthorizedException(string message, string? errorCode = null) : base(message)
    {
        ErrorCode = errorCode;
    }

    public UnauthorizedException(string message, Exception innerException) : base(message, innerException) { }
}
