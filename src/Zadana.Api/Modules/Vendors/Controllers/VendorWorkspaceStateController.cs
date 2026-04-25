using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/vendor/workspace-state")]
[Tags("Vendor App API")]
[Authorize(Policy = "VendorOnly")]
public class VendorWorkspaceStateController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentVendorService _currentVendorService;

    public VendorWorkspaceStateController(IApplicationDbContext dbContext, ICurrentVendorService currentVendorService)
    {
        _dbContext = dbContext;
        _currentVendorService = currentVendorService;
    }

    [HttpGet("{feature}")]
    public async Task<IActionResult> GetFeatureState(string feature, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var payload = await GetPayloadAsync(vendorId, feature, cancellationToken);
        return Content(payload ?? "{}", "application/json");
    }

    [HttpPut("{feature}")]
    public async Task<IActionResult> SaveFeatureState(string feature, [FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        await UpsertPayloadAsync(vendorId, feature, payload.GetRawText(), cancellationToken);
        return NoContent();
    }

    internal async Task<string?> GetPayloadAsync(Guid vendorId, string feature, CancellationToken cancellationToken)
    {
        var normalizedFeature = VendorWorkspaceState.NormalizeFeature(feature);
        return await _dbContext.VendorWorkspaceStates
            .AsNoTracking()
            .Where(state => state.VendorId == vendorId && state.Feature == normalizedFeature)
            .Select(state => state.PayloadJson)
            .FirstOrDefaultAsync(cancellationToken);
    }

    internal async Task UpsertPayloadAsync(Guid vendorId, string feature, string payloadJson, CancellationToken cancellationToken)
    {
        var normalizedFeature = VendorWorkspaceState.NormalizeFeature(feature);
        var state = await _dbContext.VendorWorkspaceStates
            .FirstOrDefaultAsync(item => item.VendorId == vendorId && item.Feature == normalizedFeature, cancellationToken);

        if (state is null)
        {
            _dbContext.VendorWorkspaceStates.Add(new VendorWorkspaceState(vendorId, normalizedFeature, payloadJson));
        }
        else
        {
            state.UpdatePayload(payloadJson);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

[Route("api/admin/vendors/{vendorId:guid}/workspace-state")]
[Tags("Admin Vendors")]
[Authorize(Policy = "AdminOnly")]
public class AdminVendorWorkspaceStateController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;

    public AdminVendorWorkspaceStateController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("{feature}")]
    public async Task<IActionResult> GetFeatureState(Guid vendorId, string feature, CancellationToken cancellationToken)
    {
        await EnsureVendorExistsAsync(vendorId, cancellationToken);
        var normalizedFeature = VendorWorkspaceState.NormalizeFeature(feature);
        var payload = await _dbContext.VendorWorkspaceStates
            .AsNoTracking()
            .Where(state => state.VendorId == vendorId && state.Feature == normalizedFeature)
            .Select(state => state.PayloadJson)
            .FirstOrDefaultAsync(cancellationToken);

        return Content(payload ?? "{}", "application/json");
    }

    [HttpPut("{feature}")]
    public async Task<IActionResult> SaveFeatureState(Guid vendorId, string feature, [FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        await EnsureVendorExistsAsync(vendorId, cancellationToken);
        var normalizedFeature = VendorWorkspaceState.NormalizeFeature(feature);
        var state = await _dbContext.VendorWorkspaceStates
            .FirstOrDefaultAsync(item => item.VendorId == vendorId && item.Feature == normalizedFeature, cancellationToken);

        if (state is null)
        {
            _dbContext.VendorWorkspaceStates.Add(new VendorWorkspaceState(vendorId, normalizedFeature, payload.GetRawText()));
        }
        else
        {
            state.UpdatePayload(payload.GetRawText());
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task EnsureVendorExistsAsync(Guid vendorId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Vendors
            .AsNoTracking()
            .AnyAsync(vendor => vendor.Id == vendorId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Vendor", vendorId);
        }
    }
}
