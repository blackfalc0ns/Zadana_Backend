using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Zadana.Api.Middleware;
using Zadana.Application.Common.Localization;

namespace Zadana.UnitTests.Common;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenRequestIsAbortedAndOperationIsCanceled_ReturnsClientClosedRequestWithoutErrorLog()
    {
        using var services = CreateServices();
        using var requestAborted = new CancellationTokenSource();
        requestAborted.Cancel();

        var context = CreateHttpContext(services);
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/vendor/orders/22222222-2222-2222-2222-222222222222";
        context.RequestAborted = requestAborted.Token;

        var logger = new RecordingLogger<ExceptionHandlingMiddleware>();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new TaskCanceledException("A task was canceled."),
            logger);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status499ClientClosedRequest);
        logger.Entries.Should().NotContain(entry => entry.LogLevel >= LogLevel.Error);
    }

    [Fact]
    public async Task InvokeAsync_WhenOperationIsCanceledWithoutRequestAbort_LogsAndReturnsServerError()
    {
        using var services = CreateServices();
        var context = CreateHttpContext(services);

        var logger = new RecordingLogger<ExceptionHandlingMiddleware>();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new TaskCanceledException("A task was canceled."),
            logger);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        logger.Entries.Should().ContainSingle(entry => entry.LogLevel == LogLevel.Error);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.Should().Contain("ServerErrorTitle");
        body.Should().Contain("traceId");
    }

    private static DefaultHttpContext CreateHttpContext(ServiceProvider services)
    {
        return new DefaultHttpContext
        {
            RequestServices = services,
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    private static ServiceProvider CreateServices()
    {
        return new ServiceCollection()
            .AddSingleton<IStringLocalizer<SharedResource>, EchoStringLocalizer<SharedResource>>()
            .BuildServiceProvider();
    }

    private sealed class EchoStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, eventId, exception, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, EventId EventId, Exception? Exception, string Message);
}
