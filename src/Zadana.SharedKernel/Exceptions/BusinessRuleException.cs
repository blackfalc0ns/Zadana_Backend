namespace Zadana.SharedKernel.Exceptions;

public class BusinessRuleException : Exception
{
    public string ErrorCode { get; }
    public object[] Args { get; }

    public BusinessRuleException(string errorCode, string message, params object[] args)
        : base(message)
    {
        ErrorCode = errorCode;
        Args = args ?? Array.Empty<object>();
    }
}
