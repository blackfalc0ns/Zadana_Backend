namespace Zadana.SharedKernel.Exceptions;

public class NotFoundException : Exception
{
    public string ErrorCode { get; }

    public NotFoundException(string entityName, object id)
        : base($"لم يتم العثور على {entityName} بالمعرف '{id}'. | {entityName} with id '{id}' was not found.")
    {
        ErrorCode = $"{entityName.ToUpperInvariant()}_NOT_FOUND";
    }
}
