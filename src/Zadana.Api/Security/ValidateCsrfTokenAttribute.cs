using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Zadana.Api.Security;

/// <summary>
/// A custom, lightweight antiforgery validation filter attribute for APIs.
/// Avoids requiring the heavy MVC Views / Microsoft.AspNetCore.Mvc.ViewFeatures
/// services that the built-in [ValidateAntiForgeryToken] filter relies on.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class ValidateCsrfTokenAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var antiforgery = context.HttpContext.RequestServices.GetService(typeof(IAntiforgery)) as IAntiforgery;
        if (antiforgery == null)
        {
            // If Antiforgery service is not registered in DI, skip validation.
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
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
    }
}
