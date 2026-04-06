using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Vendors.Requests;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.Commands.RegisterVendor;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorProfile;
using Zadana.Application.Modules.Vendors.Queries.GetVendorProfile;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/vendors")]
[Tags("Vendor App API")]
public class VendorsController : ApiControllerBase
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public VendorsController(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterVendor([FromBody] RegisterVendorRequest request)
    {
        var command = new RegisterVendorCommand(
            request.FullName,
            request.Email,
            request.Phone,
            request.Password,
            request.BusinessNameAr,
            request.BusinessNameEn,
            request.BusinessType,
            request.CommercialRegistrationNumber,
            request.ContactEmail,
            request.ContactPhone,
            request.TaxId,
            request.LogoUrl,
            request.CommercialRegisterDocumentUrl,
            request.BranchName,
            request.BranchAddressLine,
            request.BranchLatitude,
            request.BranchLongitude,
            request.BranchContactPhone,
            request.BranchDeliveryRadiusKm);

        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpGet("profile")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> GetProfile()
    {
        var result = await Sender.Send(new GetVendorProfileQuery());
        return Ok(result);
    }

    [HttpPut("profile")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateVendorProfileRequest request)
    {
        var command = new UpdateVendorProfileCommand(
            request.BusinessNameAr,
            request.BusinessNameEn,
            request.BusinessType,
            request.ContactEmail,
            request.ContactPhone,
            request.TaxId);

        var result = await Sender.Send(command);
        return Ok(new { Data = result, Message = _localizer["VendorProfileUpdated"].Value });
    }
}
