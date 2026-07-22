namespace Zadana.Api.Modules.Identity.Requests;

public sealed record CloseAccountRequest(
    string Confirmation,
    string Password,
    string? Reason = null);
