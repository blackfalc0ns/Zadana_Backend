using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Zadana.Application.Tests.Helpers;

namespace Zadana.Application.Tests.Integration;

public class RoleRegistrationOtpDispatch_IntegrationTests : IClassFixture<ZadanaWebFactory>
{
    private readonly ZadanaWebFactory _factory;
    private readonly HttpClient _client;

    public RoleRegistrationOtpDispatch_IntegrationTests(ZadanaWebFactory factory)
    {
        _factory = factory;
        _factory.OtpSink.Clear();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task VendorRegister_WithValidData_SendsOtpToVendorEmailOnly()
    {
        var csrf = await _client.GetFromJsonAsync<CsrfTokenResponse>("/api/vendors/auth/csrf");
        _client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        _client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrf!.CsrfToken);

        var phone = "011" + new Random().Next(10000000, 99999999).ToString();
        var unique = Guid.NewGuid().ToString("N");
        var email = $"vendor_{unique}@test.com";
        var body = new
        {
            fullName = "Vendor OTP Owner",
            email,
            phone,
            password = "P@ssword1234",
            businessNameAr = "متجر اختبار",
            businessNameEn = "Test Vendor",
            businessType = "Grocery",
            commercialRegistrationNumber = $"CR{unique[..10]}",
            commercialRegistrationExpiryDate = DateTime.UtcNow.AddYears(1),
            contactEmail = $"contact_{unique}@test.com",
            contactPhone = phone,
            descriptionAr = "وصف",
            descriptionEn = "Description",
            ownerName = "Vendor Owner",
            ownerEmail = $"owner_{unique}@test.com",
            ownerPhone = phone,
            idNumber = "1234567890",
            nationality = "SA",
            region = "EASTERN",
            city = "DAMMAM",
            nationalAddress = "Dammam test address",
            taxId = "300000000000003",
            licenseNumber = "LIC-123",
            bankName = "Test Bank",
            accountHolderName = "Vendor Owner",
            iban = "SA0380000000608010167519",
            swiftCode = "TESTSARI",
            payoutCycle = "Weekly",
            logoUrl = "https://example.com/logo.png",
            commercialRegisterDocumentUrl = "https://example.com/cr.pdf",
            taxDocumentUrl = "https://example.com/tax.pdf",
            licenseDocumentUrl = "https://example.com/license.pdf",
            branchName = "Main Branch",
            branchAddressLine = "Dammam branch address",
            branchLatitude = 26.3927m,
            branchLongitude = 49.9777m,
            branchContactPhone = phone,
            branchDeliveryRadiusKm = 5m
        };

        var response = await _client.PostAsJsonAsync("/api/vendors/register", body);

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        _factory.OtpSink.EmailDispatches.Should().ContainSingle(dispatch => dispatch.Recipient == email);
        _factory.OtpSink.SmsDispatches.Should().BeEmpty();
    }

    [Fact]
    public async Task DriverRegister_WithValidData_SendsOtpToDriverEmailOnly()
    {
        var phone = "012" + new Random().Next(10000000, 99999999).ToString();
        var unique = Guid.NewGuid().ToString("N");
        var email = $"driver_{unique}@test.com";
        var body = new
        {
            fullName = "Driver OTP User",
            email,
            phone,
            password = "P@ssword1234",
            vehicleType = "Car",
            nationalId = "1234567890",
            licenseNumber = $"DL{unique[..8]}",
            nationalIdExpiryDate = DateTime.UtcNow.AddYears(1),
            driverLicenseExpiryDate = DateTime.UtcNow.AddYears(1),
            vehicleLicenseNumber = $"VL{unique[..8]}",
            vehicleLicenseExpiryDate = DateTime.UtcNow.AddYears(1),
            address = "Dammam driver address",
            region = "EASTERN",
            city = "DAMMAM",
            nationalIdFrontImageUrl = "https://example.com/nid-front.png",
            nationalIdBackImageUrl = "https://example.com/nid-back.png",
            licenseImageUrl = "https://example.com/license.png",
            vehicleImageUrl = "https://example.com/vehicle.png",
            personalPhotoUrl = "https://example.com/photo.png"
        };

        var response = await _client.PostAsJsonAsync("/api/drivers/register", body);

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        _factory.OtpSink.EmailDispatches.Should().ContainSingle(dispatch => dispatch.Recipient == email);
        _factory.OtpSink.SmsDispatches.Should().BeEmpty();
    }

    private sealed record CsrfTokenResponse(string CsrfToken);
}
