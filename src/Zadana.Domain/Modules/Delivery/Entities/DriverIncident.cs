using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Delivery.Entities;

public class DriverIncident : BaseEntity
{
    public Guid DriverId { get; private set; }
    public string IncidentType { get; private set; } = null!;
    public DriverIncidentSeverity Severity { get; private set; }
    public DriverIncidentStatus Status { get; private set; }
    public string? ReviewerName { get; private set; }
    public Guid? LinkedOrderId { get; private set; }
    public string Summary { get; private set; } = null!;

    // Navigation
    public Driver Driver { get; private set; } = null!;

    private DriverIncident() { }

    public DriverIncident(
        Guid driverId,
        string incidentType,
        DriverIncidentSeverity severity,
        string summary,
        Guid? linkedOrderId = null,
        string? reviewerName = null)
    {
        DriverId = driverId;
        IncidentType = incidentType.Trim();
        Severity = severity;
        Status = DriverIncidentStatus.New;
        Summary = summary.Trim();
        LinkedOrderId = linkedOrderId;
        ReviewerName = reviewerName?.Trim();
    }

    public void MarkInReview(string? reviewerName = null)
    {
        Status = DriverIncidentStatus.InReview;
        if (reviewerName is not null) ReviewerName = reviewerName.Trim();
    }

    public void RequestDocuments()
    {
        Status = DriverIncidentStatus.WaitingDocuments;
    }

    public void Resolve()
    {
        Status = DriverIncidentStatus.Resolved;
    }
}
