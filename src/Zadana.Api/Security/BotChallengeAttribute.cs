using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Zadana.Api.Localization;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Api.Security;

/// <summary>
/// Attribute filter that requires a Cloudflare Turnstile (or compatible)
/// challenge token on the request. Reads the token from either the
/// "X-Bot-Challenge-Token" header or the "captchaToken" form/json field.
///
/// When BotChallenge:SecretKey is not configured, the filter is a no-op so
/// developers can run the API locally without enrolling in a real CAPTCHA
/// provider.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class BotChallengeAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "X-Bot-Challenge-Token";
    public const string FormFieldName = "captchaToken";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var challenge = context.HttpContext.RequestServices.GetService(typeof(IBotChallengeService)) as IBotChallengeService;
        if (challenge is null || !challenge.IsConfigured)
        {
            await next();
            return;
        }

        var token = ResolveToken(context);
        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await challenge.VerifyAsync(token, ip, context.HttpContext.RequestAborted);
        if (!result.Success)
        {
            context.Result = new ObjectResult(new
            {
                code = "BOT_CHALLENGE_FAILED",
                message = ApiLocalizedMessages.Resolve(context.HttpContext, "BOT_CHALLENGE_FAILED"),
                reason = result.FailureReason
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
            return;
        }

        await next();
    }

    private static string? ResolveToken(ActionExecutingContext context)
    {
        var http = context.HttpContext;

        if (http.Request.Headers.TryGetValue(HeaderName, out var headerToken) &&
            !string.IsNullOrWhiteSpace(headerToken))
        {
            return headerToken.ToString();
        }

        if (context.ActionArguments.Values
                .OfType<object>()
                .Select(arg => arg?.GetType().GetProperty("CaptchaToken")?.GetValue(arg) as string)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) is { } modelToken)
        {
            return modelToken;
        }

        if (http.Request.HasFormContentType &&
            http.Request.Form.TryGetValue(FormFieldName, out var formToken) &&
            !string.IsNullOrWhiteSpace(formToken))
        {
            return formToken.ToString();
        }

        return null;
    }
}
