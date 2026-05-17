using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Authorization;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Constants;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/admin/system/logs")]
[Authorize]
public class AdminSystemLogsController(IApplicationDbContext dbContext) : ApiControllerBase
{
    private const int MaxExportRows = 5000;

    [HttpGet]
    [RequireAccess(PermissionKeys.Admin.SystemView)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? sourceApp = null,
        [FromQuery] string? module = null,
        [FromQuery] bool? isSuccess = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var query = BuildFilteredQuery(search, sourceApp, module, isSuccess, fromUtc, toUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var dbLogs = await query
            .OrderByDescending(log => log.OccurredAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = dbLogs
            .Select(log => new SystemLogEntryDto(
                log.Id,
                log.OccurredAtUtc.ToString("o"),
                log.SourceApp,
                log.Module,
                log.Action,
                log.Summary,
                log.RequestPath,
                log.HttpMethod,
                log.StatusCode,
                log.IsSuccess,
                log.ActorUserId,
                log.ActorFullName,
                log.ActorEmail,
                log.ActorRole,
                log.TargetEntityType,
                log.TargetEntityId,
                log.CorrelationId,
                log.IpAddress,
                log.UserAgent,
                log.QueryString,
                log.RequestPayloadJson,
                log.MetadataJson,
                log.ErrorMessage))
            .ToList();

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return Ok(new PagedResultDto<SystemLogEntryDto>(items, pageNumber, pageSize, totalCount, totalPages));
    }

    [HttpGet("export")]
    [RequireAccess(PermissionKeys.Admin.SystemView)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? search = null,
        [FromQuery] string? sourceApp = null,
        [FromQuery] string? module = null,
        [FromQuery] bool? isSuccess = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildFilteredQuery(search, sourceApp, module, isSuccess, fromUtc, toUtc);

        var rows = await query
            .OrderByDescending(log => log.OccurredAtUtc)
            .Take(MaxExportRows)
            .Select(log => new
            {
                log.OccurredAtUtc,
                log.SourceApp,
                log.Module,
                log.Action,
                log.Summary,
                log.HttpMethod,
                log.RequestPath,
                log.StatusCode,
                log.IsSuccess,
                log.ActorFullName,
                log.ActorEmail,
                log.ActorRole,
                log.TargetEntityType,
                log.TargetEntityId,
                log.IpAddress,
                log.ErrorMessage
            })
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Source,Module,Action,Summary,HTTP Method,Path,Status Code,Success,Actor Name,Actor Email,Actor Role,Target Type,Target ID,IP Address,Error");

        foreach (var row in rows)
        {
            sb.Append(row.OccurredAtUtc.ToString("o")).Append(',');
            sb.Append(CsvEscape(row.SourceApp)).Append(',');
            sb.Append(CsvEscape(row.Module)).Append(',');
            sb.Append(CsvEscape(row.Action)).Append(',');
            sb.Append(CsvEscape(row.Summary)).Append(',');
            sb.Append(row.HttpMethod).Append(',');
            sb.Append(CsvEscape(row.RequestPath)).Append(',');
            sb.Append(row.StatusCode).Append(',');
            sb.Append(row.IsSuccess ? "Yes" : "No").Append(',');
            sb.Append(CsvEscape(row.ActorFullName)).Append(',');
            sb.Append(CsvEscape(row.ActorEmail)).Append(',');
            sb.Append(CsvEscape(row.ActorRole)).Append(',');
            sb.Append(CsvEscape(row.TargetEntityType)).Append(',');
            sb.Append(CsvEscape(row.TargetEntityId)).Append(',');
            sb.Append(CsvEscape(row.IpAddress)).Append(',');
            sb.Append(CsvEscape(row.ErrorMessage));
            sb.AppendLine();
        }

        var fileName = $"system-logs-{DateTime.UtcNow:yyyy-MM-dd}.csv";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();

        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    private IQueryable<Domain.Modules.Identity.Entities.SystemLogEntry> BuildFilteredQuery(
        string? search,
        string? sourceApp,
        string? module,
        bool? isSuccess,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        var query = dbContext.SystemLogEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(log =>
                log.Summary.Contains(term) ||
                log.Action.Contains(term) ||
                log.RequestPath.Contains(term) ||
                (log.ActorFullName != null && log.ActorFullName.Contains(term)) ||
                (log.ActorEmail != null && log.ActorEmail.Contains(term)) ||
                (log.TargetEntityId != null && log.TargetEntityId.Contains(term)) ||
                (log.TargetEntityType != null && log.TargetEntityType.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(sourceApp))
        {
            var normalizedSource = sourceApp.Trim().ToLowerInvariant();
            query = query.Where(log => log.SourceApp == normalizedSource);
        }

        if (!string.IsNullOrWhiteSpace(module))
        {
            var normalizedModule = module.Trim().ToLowerInvariant();
            query = query.Where(log => log.Module == normalizedModule);
        }

        if (isSuccess.HasValue)
        {
            query = query.Where(log => log.IsSuccess == isSuccess.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(log => log.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(log => log.OccurredAtUtc <= toUtc.Value);
        }

        return query;
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
