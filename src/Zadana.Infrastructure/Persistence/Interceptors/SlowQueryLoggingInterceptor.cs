using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Settings;

namespace Zadana.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Emits one structured warning for database commands that exceed the
/// configured threshold. Parameter values are never logged.
/// </summary>
public sealed class SlowQueryLoggingInterceptor(
    IOptions<DatabasePerformanceSettings> options,
    ILogger<SlowQueryLoggingInterceptor> logger) : DbCommandInterceptor
{
    private readonly DatabasePerformanceSettings _settings = options.Value;

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        LogIfSlow(command, eventData);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        LogIfSlow(command, eventData);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        LogIfSlow(command, eventData);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return ValueTask.FromResult(result);
    }

    private void LogIfSlow(DbCommand command, CommandExecutedEventData eventData)
    {
        if (!_settings.LogSlowQueries ||
            eventData.Duration.TotalMilliseconds < _settings.SlowQueryThresholdMilliseconds)
        {
            return;
        }

        var commandText = NormalizeCommandText(command.CommandText);
        logger.LogWarning(
            "Slow database command detected. DurationMs={DurationMs:F0}, CommandType={CommandType}, CommandText={CommandText}",
            eventData.Duration.TotalMilliseconds,
            command.CommandType,
            commandText);
    }

    private string NormalizeCommandText(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return "<empty>";
        }

        var normalized = string.Join(
            ' ',
            commandText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return normalized.Length <= _settings.MaxLoggedCommandTextLength
            ? normalized
            : normalized[.._settings.MaxLoggedCommandTextLength] + "…";
    }
}
