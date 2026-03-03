using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new 
        {
            Title = GetTitle(exception),
            Status = GetStatusCode(exception),
            Detail = exception.Message,
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
            NotFoundException => (int)HttpStatusCode.NotFound,
            _ => (int)HttpStatusCode.InternalServerError
        };

    private static string GetTitle(Exception exception) =>
        exception switch
        {
            ValidationException => "Validation Error",
            BusinessRuleException => "Business Rule Violation",
            NotFoundException => "Resource Not Found",
            _ => "Server Error"
        };

    private static object? GetErrors(Exception exception)
    {
        if (exception is ValidationException validationException)
        {
            return validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );
        }

        return null;
    }
}
