namespace Zadana.SharedKernel.Results;

public sealed class Error
{
    public string Code { get; }
    public string Message { get; }

    public Error(string code, string message)
    {
        Code = code;
        Message = message;
    }

    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "A null value was provided.");

    public static Error NotFound(string entityName, object id) =>
        new($"{entityName.ToUpperInvariant()}_NOT_FOUND", $"{entityName} with id '{id}' was not found.");

    public static Error Conflict(string code, string message) =>
        new(code, message);

    public static Error Validation(string code, string message) =>
        new(code, message);
}
