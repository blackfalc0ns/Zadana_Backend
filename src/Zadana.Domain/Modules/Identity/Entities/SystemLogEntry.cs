namespace Zadana.Domain.Modules.Identity.Entities;

public class SystemLogEntry
{
    public Guid Id { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string SourceApp { get; private set; } = null!;
    public string Module { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string Summary { get; private set; } = null!;
    public string RequestPath { get; private set; } = null!;
    public string HttpMethod { get; private set; } = null!;
    public int StatusCode { get; private set; }
    public bool IsSuccess { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string? ActorFullName { get; private set; }
    public string? ActorEmail { get; private set; }
    public string? ActorRole { get; private set; }
    public string? TargetEntityType { get; private set; }
    public string? TargetEntityId { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? QueryString { get; private set; }
    public string? RequestPayloadJson { get; private set; }
    public string? MetadataJson { get; private set; }
    public string? ErrorMessage { get; private set; }

    private SystemLogEntry()
    {
    }

    public SystemLogEntry(
        string sourceApp,
        string module,
        string action,
        string summary,
        string requestPath,
        string httpMethod,
        int statusCode,
        bool isSuccess,
        Guid? actorUserId = null,
        string? actorFullName = null,
        string? actorEmail = null,
        string? actorRole = null,
        string? targetEntityType = null,
        string? targetEntityId = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? queryString = null,
        string? requestPayloadJson = null,
        string? metadataJson = null,
        string? errorMessage = null)
    {
        Id = Guid.NewGuid();
        OccurredAtUtc = DateTime.UtcNow;
        SourceApp = NormalizeRequired(sourceApp, nameof(sourceApp));
        Module = NormalizeRequired(module, nameof(module));
        Action = NormalizeRequired(action, nameof(action));
        Summary = NormalizeRequired(summary, nameof(summary));
        RequestPath = NormalizeRequired(requestPath, nameof(requestPath));
        HttpMethod = NormalizeRequired(httpMethod, nameof(httpMethod)).ToUpperInvariant();
        StatusCode = statusCode;
        IsSuccess = isSuccess;
        ActorUserId = actorUserId;
        ActorFullName = NormalizeOptional(actorFullName);
        ActorEmail = NormalizeOptional(actorEmail);
        ActorRole = NormalizeOptional(actorRole);
        TargetEntityType = NormalizeOptional(targetEntityType);
        TargetEntityId = NormalizeOptional(targetEntityId);
        CorrelationId = NormalizeOptional(correlationId);
        IpAddress = NormalizeOptional(ipAddress);
        UserAgent = NormalizeOptional(userAgent);
        QueryString = NormalizeOptional(queryString);
        RequestPayloadJson = NormalizeOptional(requestPayloadJson);
        MetadataJson = NormalizeOptional(metadataJson);
        ErrorMessage = NormalizeOptional(errorMessage);
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", paramName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
