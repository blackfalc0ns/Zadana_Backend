using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.AddDriverIncident;

public record AddDriverIncidentCommand(
    Guid DriverId,
    string IncidentType,
    string Severity, // "medium" | "high" | "critical"
    string Summary,
    Guid? LinkedOrderId,
    string? ReviewerName) : IRequest<Guid>;

public class AddDriverIncidentCommandHandler : IRequestHandler<AddDriverIncidentCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public AddDriverIncidentCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(AddDriverIncidentCommand request, CancellationToken cancellationToken)
    {
        var driverExists = await _context.Drivers.FindAsync([request.DriverId], cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        var severity = request.Severity.ToLowerInvariant() switch
        {
            "critical" => DriverIncidentSeverity.Critical,
            "high" => DriverIncidentSeverity.High,
            _ => DriverIncidentSeverity.Medium
        };

        var incident = new DriverIncident(
            request.DriverId,
            request.IncidentType,
            severity,
            request.Summary,
            request.LinkedOrderId,
            request.ReviewerName);

        _context.DriverIncidents.Add(incident);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return incident.Id;
    }
}
