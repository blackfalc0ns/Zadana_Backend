using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Zadana.Api.Security;

/// <summary>
/// Validates the API's host-only double-submit CSRF cookie against the
/// X-XSRF-TOKEN request header.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class ValidateCsrfTokenAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var environment = context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (!ApiCsrfToken.IsValid(context.HttpContext.Request, environment))
        {
            context.Result = new ObjectResult(new
            {
                code = "INVALID_CSRF_TOKEN",
                message = "Antiforgery token validation failed."
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        return Task.CompletedTask;
    }
}
