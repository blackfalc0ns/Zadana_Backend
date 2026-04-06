namespace Zadana.SharedKernel.Exceptions;

public class ExternalServiceException : Exception
{
    public string ErrorCode { get; }

    public ExternalServiceException(string errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
