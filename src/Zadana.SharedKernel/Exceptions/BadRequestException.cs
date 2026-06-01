namespace Zadana.SharedKernel.Exceptions;

public class BadRequestException : Exception
{
    public string ErrorCode { get; }
    public object[] Args { get; }

    public BadRequestException(string errorCode, string message, params object[] args)
        : base(message)
    {
        ErrorCode = errorCode;
        Args = args ?? Array.Empty<object>();
    }
}
