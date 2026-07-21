using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Zadana.Api.Controllers;
using Zadana.Api.Security;
using Zadana.Api.Modules.Vendors.Requests;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorBanking;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorContact;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorHours;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorLegal;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorNotificationSettings;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorOperationsSettings;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorOwner;
using Zadana.Application.Modules.Vendors.Commands.RegisterVendor;
using Zadana.Application.Modules.Vendors.Commands.SubmitVendorReview;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorProfile;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorPayoutPreference;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorStore;
using Zadana.Application.Modules.Vendors.Queries.GetVendorProfile;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/vendors")]
[Tags("Vendor App API")]
public class VendorsController : ApiControllerBase
{
    private static readonly TimeSpan VendorRefreshTokenCookieLifetime = TimeSpan.FromDays(7);

    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IWebHostEnvironment _environment;

    public VendorsController(
        IStringLocalizer<SharedResource> localizer,
        IWebHostEnvironment environment)
    {
        _localizer = localizer;
        _environment = environment;
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("register")]
    [ValidateCsrfToken]
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
            request.TaxDocumentUrl,
            request.LicenseDocumentUrl,
            request.BranchName,
            request.BranchAddressLine,
            request.BranchLatitude,
            request.BranchLongitude,
            request.BranchContactPhone,
            request.BranchDeliveryRadiusKm,
            request.PayoutDay);

        var result = await Sender.Send(command);
        WriteVendorRefreshCookie(result.Tokens);
        return Ok(StripRefreshToken(result));
    }

    [HttpGet("profile")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> GetProfile()
    {
        var result = await Sender.Send(new GetVendorProfileQuery());
        return Ok(result);
    }

    private void WriteVendorRefreshCookie(TokenPairDto? tokens)
    {
        if (tokens is null || string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            return;
        }

        VendorRefreshCookie.Write(
            Response,
            _environment,
            tokens.RefreshToken,
            DateTimeOffset.UtcNow.Add(VendorRefreshTokenCookieLifetime));
    }

    private static AuthResponseDto StripRefreshToken(AuthResponseDto source)
    {
        if (source.Tokens is null)
        {
            return source;
        }

        var sanitisedPair = new TokenPairDto(source.Tokens.AccessToken, string.Empty);
        return source with { Tokens = sanitisedPair };
    }

    [HttpPost("profile/submit-for-review")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> SubmitForReview()
    {
        var result = await Sender.Send(new SubmitVendorReviewCommand());
        return Ok(new { Data = result, Message = _localizer["VendorProfileUpdated"].Value });
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
            request.NationalAddress,
            request.BranchLatitude,
            request.BranchLongitude));

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
            request.CommercialRegisterDocumentUrl,
            request.TaxDocumentUrl,
            request.LicenseDocumentUrl));

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
            request.PayoutCycle,
            request.PayoutDay));

        return Ok(new { Data = result, Message = _localizer["VendorProfileUpdated"].Value });
    }

    [HttpGet("profile/payout-preference")]
    [Authorize(Policy = "VendorOnly")]
    public async Task<IActionResult> GetPayoutPreference()
    {
        var result = await Sender.Send(new GetVendorProfileQuery());
        return Ok(new { PayoutDay = result.PayoutDay });
    }

    [HttpPut("profile/payout-preference")]
    [Authorize(Policy = "VendorOnly")]
    [ValidateCsrfToken]
    public async Task<IActionResult> UpdatePayoutPreference([FromBody] UpdateVendorPayoutPreferenceRequest? request)
    {
        var result = await Sender.Send(new UpdateVendorPayoutPreferenceCommand(request?.PayoutDay));
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
            request.NewOrdersNotificationsEnabled,
            request.NotificationSound));

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
