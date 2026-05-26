namespace Zadana.Application.Common.Interfaces;

/// <summary>
/// Records every read or update of a sensitive PII field by an
/// administrative user so we have a forensic trail for compliance
/// (PDPL / GDPR style audit).
/// </summary>
public interface IPiiAccessAuditService
{
    Task RecordAsync(
        string operation,
        string entityType,
        Guid entityId,
        string fieldName,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
