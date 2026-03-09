namespace Zadana.SharedKernel.Exceptions;

public class NotFoundException : Exception
{
    public string ErrorCode { get; }

    public NotFoundException(string entityName, object id)
        : base($"{entityName.ToUpperInvariant()}_NOT_FOUND")
    {
        ErrorCode = $"{entityName.ToUpperInvariant()}_NOT_FOUND";
    }
}
