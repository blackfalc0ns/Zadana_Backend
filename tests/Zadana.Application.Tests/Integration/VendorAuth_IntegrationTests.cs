using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Zadana.Application.Tests.Helpers;

namespace Zadana.Application.Tests.Integration;

/// <summary>
/// Integration tests for Vendor registration and auth endpoints.
/// </summary>
public class VendorAuth_IntegrationTests : IClassFixture<ZadanaWebFactory>
{
    private readonly HttpClient _client;

    public VendorAuth_IntegrationTests(ZadanaWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterVendor_WithValidData_Returns200()
    {
        var body = new
        {
            fullName = "Vendor Owner",
            email = $"vendor_{Guid.NewGuid():N}@test.com",
            phone = "013" + new Random().Next(10000000, 99999999).ToString(),
            password = "P@ssword1234",
            businessNameAr = "متجر الاختبار",
            businessNameEn = "Test Store",
            businessType = "Grocery",
            commercialRegistrationNumber = "REG" + Guid.NewGuid().ToString("N")[..8],
            contactEmail = $"contact_{Guid.NewGuid():N}@vendor.com",
            contactPhone = "013" + new Random().Next(10000000, 99999999).ToString(),
            branchName = "Main Branch",
            branchAddressLine = "123 Main St",
            branchLatitude = 30.0444m,
            branchLongitude = 31.2357m,
            branchContactPhone = "013" + new Random().Next(10000000, 99999999).ToString(),
            branchDeliveryRadiusKm = 5.0m
        };

        var response = await _client.PostAsJsonAsync("/api/vendors/register", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("accessToken", "vendor registration must return tokens");
    }

    [Fact]
    public async Task RegisterVendor_WithMissingBusinessName_Returns400()
    {
        var body = new
        {
            fullName = "Incomplete Vendor",
            email = $"inc_{Guid.NewGuid():N}@test.com",
            phone = "01311111111",
            password = "P@ssword1234"
            // Missing: businessNameAr, businessNameEn, etc.
        };

        var response = await _client.PostAsJsonAsync("/api/vendors/register", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VendorLogin_WithValidCredentials_ReturnsToken()
    {
        // First register the vendor
        var email = $"vlogin_{Guid.NewGuid():N}@test.com";
        var phone = "018" + new Random().Next(10000000, 99999999).ToString();
        var password = "P@ssword1234";

        await _client.PostAsJsonAsync("/api/vendors/register", new
        {
            fullName = "Login Vendor",
            email,
            phone,
            password,
            businessNameAr = "متجر تسجيل",
            businessNameEn = "Login Store",
            businessType = "Grocery",
            commercialRegistrationNumber = "REG" + Guid.NewGuid().ToString("N")[..8],
            contactEmail = email,
            contactPhone = phone,
            branchName = "Branch One",
            branchAddressLine = "St 1",
            branchLatitude = 30.0m,
            branchLongitude = 31.0m,
            branchContactPhone = phone,
            branchDeliveryRadiusKm = 3.0m
        });

        // Then login
        var loginBody = new { identifier = email, password };
        var response = await _client.PostAsJsonAsync("/api/vendors/auth/login", loginBody);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("accessToken");
    }

    [Fact]
    public async Task GetVendorMe_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/vendors/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
