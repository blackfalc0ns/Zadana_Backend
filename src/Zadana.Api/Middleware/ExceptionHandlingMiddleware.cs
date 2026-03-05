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
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ExceptionHandlingMiddleware(
        RequestDelegate next, 
        ILogger<ExceptionHandlingMiddleware> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _next = next;
        _logger = logger;
        _localizer = localizer;
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
            await HandleExceptionAsync(context, ex, _localizer);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, IStringLocalizer<SharedResource> localizer)
    {
        context.Response.ContentType = "application/json";

        string message = exception.Message;

        // Try to localize based on ErrorCode if it's a known exception type
        if (exception is BusinessRuleException brEx)
        {
            var localized = localizer[brEx.ErrorCode];
            if (!localized.ResourceNotFound)
            {
                message = localized.Value;
            }
        }
        else if (exception is NotFoundException nfEx)
        {
            var localized = localizer[nfEx.ErrorCode];
            if (!localized.ResourceNotFound)
            {
                message = localized.Value;
            }
        }
        else if (exception is UnauthorizedException)
        {
             var localized = localizer["USER_NOT_AUTHENTICATED"];
             if (!localized.ResourceNotFound)
             {
                 message = localized.Value;
             }
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
            Title = GetTitle(exception),
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

    private static string GetTitle(Exception exception) =>
        exception switch
        {
            ValidationException => "Validation Error",
            BusinessRuleException => "Business Rule Violation",
            UnauthorizedException => "Unauthorized",
            NotFoundException => "Resource Not Found",
            _ => "Server Error"
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
