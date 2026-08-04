using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/vendor/workspace-state")]
[Tags("Vendor App API")]
[Authorize(Policy = "VendorOnly")]
public class VendorWorkspaceStateController : ApiControllerBase
{
    private static readonly string[] CatalogHybridCacheTags =
    [
        CacheTagNames.Catalog,
        CacheTagNames.CatalogFilters,
        CacheTagNames.Home
    ];

    private static readonly string[] PublicOutputCacheTags =
    [
        "catalog-browse",
        "home-public"
    ];

    private readonly IApplicationDbContext _dbContext;
    private readonly ICacheInvalidator _cacheInvalidator;
    private readonly ICurrentVendorService _currentVendorService;
    private readonly IOutputCacheStore _outputCacheStore;

    public VendorWorkspaceStateController(
        IApplicationDbContext dbContext,
        ICacheInvalidator cacheInvalidator,
        ICurrentVendorService currentVendorService,
        IOutputCacheStore outputCacheStore)
    {
        _dbContext = dbContext;
        _cacheInvalidator = cacheInvalidator;
        _currentVendorService = currentVendorService;
        _outputCacheStore = outputCacheStore;
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
        var payloadJson = payload.GetRawText();
        await UpsertPayloadAsync(vendorId, feature, payloadJson, cancellationToken);
        return Content(payloadJson, "application/json");
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
        await InvalidateCustomerReadCachesAsync(cancellationToken);
    }

    private async Task InvalidateCustomerReadCachesAsync(CancellationToken cancellationToken)
    {
        await _cacheInvalidator.RemoveByTagsAsync(CatalogHybridCacheTags, cancellationToken);

        foreach (var tag in PublicOutputCacheTags)
        {
            await _outputCacheStore.EvictByTagAsync(tag, cancellationToken);
        }
    }
}

[Route("api/admin/vendors/{vendorId:guid}/workspace-state")]
[Tags("Admin Vendors")]
[Authorize(Policy = "AdminOnly")]
public class AdminVendorWorkspaceStateController : ApiControllerBase
{
    private static readonly string[] CatalogHybridCacheTags =
    [
        CacheTagNames.Catalog,
        CacheTagNames.CatalogFilters,
        CacheTagNames.Home
    ];

    private static readonly string[] PublicOutputCacheTags =
    [
        "catalog-browse",
        "home-public"
    ];

    private readonly IApplicationDbContext _dbContext;
    private readonly ICacheInvalidator _cacheInvalidator;
    private readonly IOutputCacheStore _outputCacheStore;

    public AdminVendorWorkspaceStateController(
        IApplicationDbContext dbContext,
        ICacheInvalidator cacheInvalidator,
        IOutputCacheStore outputCacheStore)
    {
        _dbContext = dbContext;
        _cacheInvalidator = cacheInvalidator;
        _outputCacheStore = outputCacheStore;
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
        var payloadJson = payload.GetRawText();
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
        await InvalidateCustomerReadCachesAsync(cancellationToken);
        return Content(payloadJson, "application/json");
    }

    private async Task InvalidateCustomerReadCachesAsync(CancellationToken cancellationToken)
    {
        await _cacheInvalidator.RemoveByTagsAsync(CatalogHybridCacheTags, cancellationToken);

        foreach (var tag in PublicOutputCacheTags)
        {
            await _outputCacheStore.EvictByTagAsync(tag, cancellationToken);
        }
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
