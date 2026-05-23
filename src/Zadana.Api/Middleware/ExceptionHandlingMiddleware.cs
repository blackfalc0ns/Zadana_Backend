using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
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
        catch (OperationCanceledException ex) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug(
                ex,
                "Request was canceled before completion: {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }
        }
        catch (Exception ex)
        {
            // Log the full exception (with stack trace) using the structured
            // logger; do not interpolate StackTrace into the message template
            // because that produces enormous log lines and duplicates the
            // exception details that the logger already records.
            _logger.LogError(
                ex,
                "Unhandled exception while processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            var localizer = context.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
            await HandleExceptionAsync(context, ex, localizer);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, IStringLocalizer<SharedResource> localizer)
    {
        context.Response.ContentType = "application/json";

        var problemDetails = CreateProblemDetails(context, exception, localizer);
        context.Response.StatusCode = problemDetails.Status ?? (int)HttpStatusCode.InternalServerError;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, options));
    }

    private static ProblemDetails CreateProblemDetails(HttpContext context, Exception exception, IStringLocalizer<SharedResource> localizer)
    {
        if (exception is ValidationException validationException)
        {
            var validationProblem = new ValidationProblemDetails(validationException.Errors)
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = GetLocalizedResource("ValidationErrorTitle", context, localizer),
                Detail = validationException.Errors.SelectMany(e => e.Value).FirstOrDefault()
                    ?? GetLocalizedResource("ValidationErrorTitle", context, localizer),
                Instance = context.Request.Path
            };

            validationProblem.Extensions["traceId"] = context.TraceIdentifier;
            validationProblem.Extensions["errorCode"] = "VALIDATION_ERROR";
            return validationProblem;
        }

        var problemDetails = new ProblemDetails
        {
            Status = GetStatusCode(exception),
            Title = GetTitle(exception, context, localizer),
            Detail = ResolveDetail(exception, context, localizer),
            Instance = context.Request.Path
        };

        var errorCode = GetErrorCode(exception);
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            problemDetails.Extensions["errorCode"] = errorCode;
        }

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;
        return problemDetails;
    }

    private static int GetStatusCode(Exception exception) =>
        exception switch
        {
            ValidationException => (int)HttpStatusCode.BadRequest,
            BadRequestException => (int)HttpStatusCode.BadRequest,
            BusinessRuleException => (int)HttpStatusCode.Conflict,
            ExternalServiceException => (int)HttpStatusCode.BadGateway,
            UnauthorizedException => (int)HttpStatusCode.Unauthorized,
            UnauthorizedAccessException => (int)HttpStatusCode.Forbidden,
            ForbiddenAccessException => (int)HttpStatusCode.Forbidden,
            NotFoundException => (int)HttpStatusCode.NotFound,
            _ => (int)HttpStatusCode.InternalServerError
        };

    private static string GetTitle(Exception exception, HttpContext context, IStringLocalizer<SharedResource> localizer) =>
        exception switch
        {
            ValidationException => GetLocalizedResource("ValidationErrorTitle", context, localizer),
            BadRequestException => GetLocalizedResource("ValidationErrorTitle", context, localizer),
            BusinessRuleException => GetLocalizedResource("BusinessRuleViolationTitle", context, localizer),
            ExternalServiceException => GetLocalizedResource("ExternalServiceErrorTitle", context, localizer),
            UnauthorizedException => GetLocalizedResource("UnauthorizedTitle", context, localizer),
            UnauthorizedAccessException => GetLocalizedResource("UnauthorizedTitle", context, localizer),
            ForbiddenAccessException => GetLocalizedResource("UnauthorizedTitle", context, localizer),
            NotFoundException => GetLocalizedResource("ResourceNotFoundTitle", context, localizer),
            _ => GetLocalizedResource("ServerErrorTitle", context, localizer)
        };

    private static string ResolveDetail(Exception exception, HttpContext context, IStringLocalizer<SharedResource> localizer)
    {
        var message = exception switch
        {
            BusinessRuleException bre => ResolveByErrorCode(bre.ErrorCode, bre.Message, context, localizer),
            BadRequestException bad => ResolveByErrorCode(bad.ErrorCode, bad.Message, context, localizer),
            NotFoundException nf => ResolveByErrorCode(nf.ErrorCode, nf.Message, context, localizer),
            ExternalServiceException ext => ResolveByErrorCode(ext.ErrorCode, ext.Message, context, localizer),
            ForbiddenAccessException => exception.Message,
            UnauthorizedAccessException => exception.Message,
            UnauthorizedException unauthorizedException when
                string.IsNullOrWhiteSpace(unauthorizedException.Message) ||
                unauthorizedException.Message == "Exception of type 'Zadana.SharedKernel.Exceptions.UnauthorizedException' was thrown."
                => GetLocalizedResource("USER_NOT_AUTHENTICATED", context, localizer),
            UnauthorizedException => exception.Message,
            _ => GetLocalizedResource("ServerErrorMessage", context, localizer)
        };

        // Support inline AR|EN format as fallback
        if (!string.IsNullOrWhiteSpace(message) && message.Contains('|'))
        {
            var parts = message.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
            {
                message = PrefersEnglish(context) ? parts[1] : parts[0];
            }
        }

        return message;
    }

    /// <summary>
    /// Resolves an exception message by first checking .resx resource files
    /// using the ErrorCode as key. Falls back to the inline message if no resource found.
    /// </summary>
    private static string ResolveByErrorCode(
        string errorCode,
        string fallbackMessage,
        HttpContext context,
        IStringLocalizer<SharedResource> localizer)
    {
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            var localized = GetLocalizedResource(errorCode, context, localizer);
            if (!string.Equals(localized, errorCode, StringComparison.Ordinal))
            {
                return localized;
            }
        }

        // Fallback: use the inline message (which may contain AR|EN format)
        return fallbackMessage;
    }

    private static string? GetErrorCode(Exception exception) =>
        exception switch
        {
            BadRequestException badRequestException => badRequestException.ErrorCode,
            BusinessRuleException businessRuleException => businessRuleException.ErrorCode,
            NotFoundException notFoundException => notFoundException.ErrorCode,
            ExternalServiceException externalServiceException => externalServiceException.ErrorCode,
            ForbiddenAccessException forbiddenAccessException => forbiddenAccessException.ErrorCode,
            UnauthorizedException unauthorizedException => unauthorizedException.ErrorCode,
            _ => null
        };

    private static string GetLocalizedResource(string key, HttpContext context, IStringLocalizer<SharedResource> localizer)
    {
        var value = PrefersEnglish(context)
            ? LocalizedMessages.GetEn(key)
            : LocalizedMessages.GetAr(key);

        if (!string.Equals(value, key, StringComparison.Ordinal))
        {
            return value;
        }

        return localizer[key];
    }

    private static bool PrefersEnglish(HttpContext context)
    {
        var language = context.Request.Headers["Accept-Language"].ToString().ToLowerInvariant();
        return language.Contains("en");
    }
}
