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
            _logger.LogError(
                ex,
                "An unhandled exception has occurred: {Message}. StackTrace: {StackTrace}",
                ex.Message,
                ex.StackTrace);

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
                Title = localizer["ValidationErrorTitle"],
                Detail = validationException.Errors.SelectMany(e => e.Value).FirstOrDefault() ?? localizer["ValidationErrorTitle"],
                Instance = context.Request.Path
            };

            validationProblem.Extensions["traceId"] = context.TraceIdentifier;
            validationProblem.Extensions["errorCode"] = "VALIDATION_ERROR";
            return validationProblem;
        }

        var problemDetails = new ProblemDetails
        {
            Status = GetStatusCode(exception),
            Title = GetTitle(exception, localizer),
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

    private static string GetTitle(Exception exception, IStringLocalizer<SharedResource> localizer) =>
        exception switch
        {
            ValidationException => localizer["ValidationErrorTitle"],
            BadRequestException => localizer["ValidationErrorTitle"],
            BusinessRuleException => localizer["BusinessRuleViolationTitle"],
            ExternalServiceException => localizer["ExternalServiceErrorTitle"],
            UnauthorizedException => localizer["UnauthorizedTitle"],
            UnauthorizedAccessException => localizer["UnauthorizedTitle"],
            ForbiddenAccessException => localizer["UnauthorizedTitle"],
            NotFoundException => localizer["ResourceNotFoundTitle"],
            _ => localizer["ServerErrorTitle"]
        };

    private static string ResolveDetail(Exception exception, HttpContext context, IStringLocalizer<SharedResource> localizer)
    {
        var message = exception switch
        {
            BusinessRuleException bre => ResolveByErrorCode(bre.ErrorCode, bre.Message, localizer),
            BadRequestException bad => ResolveByErrorCode(bad.ErrorCode, bad.Message, localizer),
            NotFoundException nf => ResolveByErrorCode(nf.ErrorCode, nf.Message, localizer),
            ExternalServiceException ext => ResolveByErrorCode(ext.ErrorCode, ext.Message, localizer),
            ForbiddenAccessException => exception.Message,
            UnauthorizedAccessException => exception.Message,
            UnauthorizedException unauthorizedException when
                string.IsNullOrWhiteSpace(unauthorizedException.Message) ||
                unauthorizedException.Message == "Exception of type 'Zadana.SharedKernel.Exceptions.UnauthorizedException' was thrown."
                => localizer["USER_NOT_AUTHENTICATED"],
            UnauthorizedException => exception.Message,
            _ => localizer["ServerErrorMessage"]
        };

        // Support inline AR|EN format as fallback
        if (!string.IsNullOrWhiteSpace(message) && message.Contains('|'))
        {
            var language = context.Request.Headers["Accept-Language"].ToString().ToLowerInvariant();
            var isArabic = language.Contains("ar");
            var parts = message.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
            {
                message = isArabic ? parts[0] : parts[1];
            }
        }

        return message;
    }

    /// <summary>
    /// Resolves an exception message by first checking .resx resource files
    /// using the ErrorCode as key. Falls back to the inline message if no resource found.
    /// </summary>
    private static string ResolveByErrorCode(string errorCode, string fallbackMessage, IStringLocalizer<SharedResource> localizer)
    {
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            var localized = localizer[errorCode];
            if (!localized.ResourceNotFound)
            {
                return localized.Value;
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
            _ => null
        };
}
