using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Commands.AddDriverIncident;
using Zadana.Application.Modules.Delivery.Commands.AddDriverNote;
using Zadana.Application.Modules.Delivery.Commands.ReactivateDriver;
using Zadana.Application.Modules.Delivery.Commands.ReviewDriver;
using Zadana.Application.Modules.Delivery.Commands.SuspendDriver;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Delivery.Controllers;

[Route("api/admin/drivers")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin – Driver Management")]
public class AdminDriversController : ApiControllerBase
{
    private readonly IDriverReadService _driverReadService;

    public AdminDriversController(IDriverReadService driverReadService)
    {
        _driverReadService = driverReadService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDrivers(
        [FromQuery] string? search,
        [FromQuery] string? city,
        [FromQuery] string? status,
        [FromQuery] string? verificationStatus,
        [FromQuery] string? vehicleType,
        [FromQuery] string? performance,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _driverReadService.GetAdminDriversAsync(
            search, city, status, verificationStatus, vehicleType, performance,
            Math.Max(1, page), Math.Clamp(pageSize, 1, 100), cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDriverDetail(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _driverReadService.GetAdminDriverDetailAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> ReviewDriver(
        Guid id,
        [FromBody] ReviewDriverRequest request,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId
            ?? throw new UnauthorizedException("ADMIN_NOT_AUTHENTICATED");

        await Sender.Send(new ReviewDriverCommand(id, request.Action, request.Note, userId), cancellationToken);
        return Ok(new { message = "Driver review action applied successfully" });
    }

    [HttpPost("{id:guid}/suspend")]
    public async Task<IActionResult> SuspendDriver(
        Guid id,
        [FromBody] SuspendDriverRequest? request,
        CancellationToken cancellationToken = default)
    {
        await Sender.Send(new SuspendDriverCommand(id, request?.Reason), cancellationToken);
        return Ok(new { message = "Driver suspended successfully" });
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> ReactivateDriver(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await Sender.Send(new ReactivateDriverCommand(id), cancellationToken);
        return Ok(new { message = "Driver reactivated successfully" });
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> AddNote(
        Guid id,
        [FromBody] AddDriverNoteRequest request,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId
            ?? throw new UnauthorizedException("ADMIN_NOT_AUTHENTICATED");

        var noteId = await Sender.Send(
            new AddDriverNoteCommand(id, userId, request.Message), cancellationToken);

        return Ok(new { id = noteId, message = "Note added successfully" });
    }

    [HttpPost("{id:guid}/incidents")]
    public async Task<IActionResult> AddIncident(
        Guid id,
        [FromBody] AddDriverIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        var incidentId = await Sender.Send(
            new AddDriverIncidentCommand(
                id, request.IncidentType, request.Severity,
                request.Summary, request.LinkedOrderId, request.ReviewerName),
            cancellationToken);

        return Ok(new { id = incidentId, message = "Incident recorded successfully" });
    }
}

public record ReviewDriverRequest(string Action, string? Note);
public record SuspendDriverRequest(string? Reason);
public record AddDriverNoteRequest(string Message);
public record AddDriverIncidentRequest(
    string IncidentType, string Severity, string Summary,
    Guid? LinkedOrderId, string? ReviewerName);
