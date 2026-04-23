using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Api.Modules.Delivery.Controllers;

[Route("api/admin/delivery-zones")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin – Delivery Zones")]
public class AdminDeliveryZonesController : ApiControllerBase
{
    private readonly IDriverReadService _driverReadService;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public AdminDeliveryZonesController(
        IDriverReadService driverReadService,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork)
    {
        _driverReadService = driverReadService;
        _context = context;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<IActionResult> GetZones(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var zones = activeOnly
            ? await _driverReadService.GetActiveZonesAsync(cancellationToken)
            : await _driverReadService.GetAllZonesAsync(cancellationToken);

        return Ok(zones);
    }

    [HttpPost]
    public async Task<IActionResult> CreateZone(
        [FromBody] CreateDeliveryZoneRequest request,
        CancellationToken cancellationToken = default)
    {
        var zone = new DeliveryZone(
            request.City, request.Name,
            request.CenterLat, request.CenterLng, request.RadiusKm);

        _context.DeliveryZones.Add(zone);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new DeliveryZoneDto(
            zone.Id, zone.City, zone.Name,
            zone.CenterLat, zone.CenterLng, zone.RadiusKm, zone.IsActive));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateZone(
        Guid id,
        [FromBody] UpdateDeliveryZoneRequest request,
        CancellationToken cancellationToken = default)
    {
        var zone = await _context.DeliveryZones.FindAsync([id], cancellationToken);
        if (zone is null) return NotFound();

        zone.Update(request.City, request.Name, request.CenterLat, request.CenterLng, request.RadiusKm);

        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value) zone.Activate();
            else zone.Deactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new DeliveryZoneDto(
            zone.Id, zone.City, zone.Name,
            zone.CenterLat, zone.CenterLng, zone.RadiusKm, zone.IsActive));
    }
}

public record CreateDeliveryZoneRequest(
    string City, string Name,
    decimal CenterLat, decimal CenterLng, decimal RadiusKm);

public record UpdateDeliveryZoneRequest(
    string City, string Name,
    decimal CenterLat, decimal CenterLng, decimal RadiusKm,
    bool? IsActive);
