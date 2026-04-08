using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrands;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategories;
using Zadana.Application.Modules.Catalog.Queries.GetMasterProducts;
using Zadana.Application.Modules.Catalog.Queries.Units.GetUnits;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/vendor/catalog")]
[Tags("Catalog Vendors")]
[Authorize(Roles = "Vendor")]
public class VendorCatalogController : ApiControllerBase
{
    private readonly ICurrentVendorService _currentVendorService;
    private readonly ICatalogRequestReadService _catalogRequestReadService;
    private readonly IApplicationDbContext _context;

    public VendorCatalogController(
        ICurrentVendorService currentVendorService,
        ICatalogRequestReadService catalogRequestReadService,
        IApplicationDbContext context)
    {
        _currentVendorService = currentVendorService;
        _catalogRequestReadService = catalogRequestReadService;
        _context = context;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromQuery] bool includeInactive = false)
    {
        var result = await Sender.Send(new GetCategoriesQuery(includeInactive));
        return Ok(result);
    }

    [HttpGet("brands")]
    public async Task<IActionResult> GetBrands([FromQuery] bool includeInactive = false)
    {
        var result = await Sender.Send(new GetBrandsQuery(includeInactive));
        return Ok(result);
    }

    [HttpGet("units")]
    public async Task<IActionResult> GetUnits([FromQuery] bool includeInactive = false)
    {
        var result = await Sender.Send(new GetUnitsQuery(includeInactive));
        return Ok(result);
    }

    [HttpGet("master-products")]
    public async Task<IActionResult> GetMasterProducts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? brandId = null)
    {
        var vendorId = await _currentVendorService.TryGetVendorIdAsync(HttpContext.RequestAborted);

        var result = await Sender.Send(new GetMasterProductsQuery(
            searchTerm, 
            categoryId, 
            brandId, 
            ProductStatus.Active, 
            vendorId, 
            pageNumber, 
            pageSize));
        return Ok(result);
    }

    [HttpGet("request-center")]
    public async Task<IActionResult> GetMyCatalogRequests([FromQuery] GetCatalogRequestCenterRequest request)
    {
        var vendorId = await _currentVendorService.TryGetVendorIdAsync(HttpContext.RequestAborted);
        if (!vendorId.HasValue)
        {
            return Ok(new { items = Array.Empty<CatalogRequestListItemDto>() });
        }

        var result = await _catalogRequestReadService.GetVendorRequestsAsync(
            vendorId.Value,
            request.Type,
            request.Status,
            request.PageNumber,
            request.PageSize,
            HttpContext.RequestAborted);

        return Ok(result);
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetCatalogNotifications()
    {
        var vendorId = await _currentVendorService.TryGetVendorIdAsync(HttpContext.RequestAborted);
        if (!vendorId.HasValue)
        {
            return Ok(Array.Empty<VendorCatalogNotificationDto>());
        }

        var vendor = await _context.Vendors.FindAsync([vendorId.Value], HttpContext.RequestAborted);
        if (vendor is null)
        {
            return Ok(Array.Empty<VendorCatalogNotificationDto>());
        }

        var notifications = await _catalogRequestReadService.GetVendorNotificationsAsync(vendor.UserId, HttpContext.RequestAborted);
        return Ok(notifications);
    }
}
