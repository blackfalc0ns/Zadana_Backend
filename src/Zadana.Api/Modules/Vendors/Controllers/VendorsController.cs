using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Vendors.Requests;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorBanking;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorContact;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorHours;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorLegal;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorNotificationSettings;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorOperationsSettings;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorOwner;
using Zadana.Application.Modules.Vendors.Commands.RegisterVendor;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorProfile;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorStore;
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
            request.CommercialRegistrationExpiryDate,
            request.ContactEmail,
            request.ContactPhone,
            request.DescriptionAr,
            request.DescriptionEn,
            request.OwnerName,
            request.OwnerEmail,
            request.OwnerPhone,
            request.IdNumber,
            request.Nationality,
            request.Region,
            request.City,
            request.NationalAddress,
            request.TaxId,
            request.LicenseNumber,
            request.BankName,
            request.AccountHolderName,
            request.Iban,
            request.SwiftCode,
            request.PayoutCycle,
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

    [HttpPut("profile/store")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> UpdateStore([FromBody] UpdateVendorStoreRequest request)
    {
        var result = await Sender.Send(new UpdateVendorStoreCommand(
            request.BusinessNameAr,
            request.BusinessNameEn,
            request.BusinessType,
            request.ContactEmail,
            request.ContactPhone,
            request.DescriptionAr,
            request.DescriptionEn,
            request.LogoUrl,
            request.CommercialRegisterDocumentUrl,
            request.Region,
            request.City,
            request.NationalAddress,
            request.CommercialRegistrationNumber));

        return Ok(new { Data = result, Message = _localizer["VendorProfileUpdated"].Value });
    }

    [HttpPut("profile/owner")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> UpdateOwner([FromBody] UpdateVendorOwnerRequest request)
    {
        var result = await Sender.Send(new UpdateVendorOwnerCommand(
            request.OwnerName,
            request.OwnerEmail,
            request.OwnerPhone,
            request.IdNumber,
            request.Nationality));

        return Ok(new { Data = result, Message = _localizer["VendorProfileUpdated"].Value });
    }

    [HttpPut("profile/contact")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> UpdateContact([FromBody] UpdateVendorContactRequest request)
    {
        var result = await Sender.Send(new UpdateVendorContactCommand(
            request.Region,
            request.City,
            request.NationalAddress));

        return Ok(new { Data = result, Message = _localizer["VendorProfileUpdated"].Value });
    }

    [HttpPut("profile/legal")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> UpdateLegal([FromBody] UpdateVendorLegalRequest request)
    {
        var result = await Sender.Send(new UpdateVendorLegalCommand(
            request.CommercialRegistrationNumber,
            request.CommercialRegistrationExpiryDate,
            request.TaxId,
            request.LicenseNumber,
            request.CommercialRegisterDocumentUrl));

        return Ok(new { Data = result, Message = _localizer["VendorProfileUpdated"].Value });
    }

    [HttpPut("profile/banking")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> UpdateBanking([FromBody] UpdateVendorBankingRequest request)
    {
        var result = await Sender.Send(new UpdateVendorBankingCommand(
            request.BankName,
            request.AccountHolderName,
            request.Iban,
            request.SwiftCode,
            request.PayoutCycle));

        return Ok(new { Data = result, Message = _localizer["VendorProfileUpdated"].Value });
    }

    [HttpPut("profile/hours")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> UpdateHours([FromBody] UpdateVendorHoursRequest request)
    {
        var result = await Sender.Send(new UpdateVendorHoursCommand(
            request.Hours.Select(item => new UpdateVendorHoursItem(
                item.DayOfWeek,
                item.OpenTime,
                item.CloseTime,
                item.IsOpen)).ToList()));

        return Ok(new { Data = result, Message = _localizer["VendorProfileUpdated"].Value });
    }

    [HttpPut("profile/operations-settings")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> UpdateOperationsSettings([FromBody] UpdateVendorOperationsSettingsRequest request)
    {
        var result = await Sender.Send(new UpdateVendorOperationsSettingsCommand(
            request.AcceptOrders,
            request.MinimumOrderAmount,
            request.PreparationTimeMinutes));

        return Ok(new { Data = result, Message = _localizer["VendorProfileUpdated"].Value });
    }

    [HttpPut("profile/notification-settings")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> UpdateNotificationSettings([FromBody] UpdateVendorNotificationSettingsRequest request)
    {
        var result = await Sender.Send(new UpdateVendorNotificationSettingsCommand(
            request.EmailNotificationsEnabled,
            request.SmsNotificationsEnabled,
            request.NewOrdersNotificationsEnabled));

        return Ok(new { Data = result, Message = _localizer["VendorProfileUpdated"].Value });
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
