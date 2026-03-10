using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.SharedKernel.Exceptions;
using ValidationException = Zadana.Application.Common.Exceptions.ValidationException;

namespace Zadana.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next, 
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception has occurred.");
            var localizer = context.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
            await HandleExceptionAsync(context, ex, localizer);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, IStringLocalizer<SharedResource> localizer)
    {
        context.Response.ContentType = "application/json";

        string message;

        // --- Resolve Detail Message based on exception type ---
        // Handlers already inject their own IStringLocalizer and pass localized messages 
        // in the exception. We trust the exception's message and only override for specific cases.

        if (exception is ValidationException validationEx)
        {
            // Extract the first specific validation error for a clear message
            var firstError = validationEx.Errors
                .SelectMany(e => e.Value)
                .FirstOrDefault();
            
            message = firstError ?? localizer["ValidationErrorTitle"];
        }
        else if (exception is BusinessRuleException)
        {
            // The handler already set the localized message via _localizer in the exception
            message = exception.Message;
        }
        else if (exception is NotFoundException)
        {
            // The handler already set the localized message via ErrorCode in the exception
            message = exception.Message;
        }
        else if (exception is UnauthorizedException)
        {
            // The handler already set the localized message (e.g. AccountNotFound, InvalidCredentials)
            // Only fallback to generic message if no specific message was set
            if (string.IsNullOrWhiteSpace(exception.Message) || 
                exception.Message == "Exception of type 'Zadana.SharedKernel.Exceptions.UnauthorizedException' was thrown.")
            {
                message = localizer["USER_NOT_AUTHENTICATED"];
            }
            else
            {
                message = exception.Message;
            }
        }
        else
        {
            // 500 Internal Server Error: mask with generic localized message
            message = localizer["ServerErrorMessage"];
        }

        // Fallback or Legacy: Bilingual splitting if still present
        if (!string.IsNullOrWhiteSpace(message) && message.Contains('|'))
        {
            var language = context.Request.Headers["Accept-Language"].ToString().ToLower();
            var isArabic = language.Contains("ar");
            var parts = message.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
            {
                message = isArabic ? parts[0] : parts[1];
            }
        }

        var response = new 
        {
            Title = GetTitle(exception, localizer),
            Status = GetStatusCode(exception),
            Detail = message,
            Errors = GetErrors(exception)
        };

        context.Response.StatusCode = response.Status;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    private static int GetStatusCode(Exception exception) =>
        exception switch
        {
            ValidationException => (int)HttpStatusCode.BadRequest,
            BusinessRuleException => (int)HttpStatusCode.BadRequest,
            UnauthorizedException => (int)HttpStatusCode.Unauthorized,
            NotFoundException => (int)HttpStatusCode.NotFound,
            _ => (int)HttpStatusCode.InternalServerError
        };

    private static string GetTitle(Exception exception, IStringLocalizer<SharedResource> localizer) =>
        exception switch
        {
            ValidationException => localizer["ValidationErrorTitle"],
            BusinessRuleException => localizer["BusinessRuleViolationTitle"],
            UnauthorizedException => localizer["UnauthorizedTitle"],
            NotFoundException => localizer["ResourceNotFoundTitle"],
            _ => localizer["ServerErrorTitle"]
        };

    private static object? GetErrors(Exception exception)
    {
        if (exception is ValidationException validationException)
        {
            return validationException.Errors;
        }

        return null;
    }
}
