using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Zadana.Application.Tests.Helpers;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Application.Tests.Integration;

/// <summary>
/// Integration tests for Vendor registration and auth endpoints.
/// </summary>
public class VendorAuth_IntegrationTests : IClassFixture<ZadanaWebFactory>
{
    private readonly ZadanaWebFactory _factory;
    private readonly HttpClient _client;

    public VendorAuth_IntegrationTests(ZadanaWebFactory factory)
    {
        _factory = factory;
        _factory.OtpSink.Clear();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterVendor_WithValidData_Returns200WithoutTokens()
    {
        await EnsureCsrfAsync();
        var email = $"vendor_{Guid.NewGuid():N}@test.com";
        var body = BuildVendorRegisterBody(email);

        var response = await _client.PostAsJsonAsync("/api/vendors/register", body);

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("isVerified").GetBoolean().Should().BeFalse();
        doc.RootElement.TryGetProperty("tokens", out var tokens).Should().BeTrue();
        tokens.ValueKind.Should().Be(JsonValueKind.Null);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        (await userManager.FindByEmailAsync(email)).Should().BeNull();
    }

    [Fact]
    public async Task RegisterVendor_WithMissingBusinessName_Returns400()
    {
        await EnsureCsrfAsync();
        var body = new
        {
            fullName = "Incomplete Vendor",
            email = $"inc_{Guid.NewGuid():N}@test.com",
            phone = "01311111111",
            password = "P@ssword1234"
        };

        var response = await _client.PostAsJsonAsync("/api/vendors/register", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VendorLogin_WithValidCredentials_ReturnsToken()
    {
        await EnsureCsrfAsync();
        var email = $"vlogin_{Guid.NewGuid():N}@test.com";
        var password = "P@ssword1234";

        var registerResponse = await _client.PostAsJsonAsync(
            "/api/vendors/register",
            BuildVendorRegisterBody(email, password));
        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK, registerContent);

        var otpCode = _factory.OtpSink.EmailDispatches
            .Single(dispatch => dispatch.Recipient == email)
            .OtpCode;

        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/vendors/auth/verify-otp",
            new { identifier = email, otpCode });
        var verifyContent = await verifyResponse.Content.ReadAsStringAsync();
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK, verifyContent);

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

    private async Task EnsureCsrfAsync()
    {
        var csrf = await _client.GetFromJsonAsync<CsrfTokenResponse>("/api/vendors/auth/csrf");
        _client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        _client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrf!.CsrfToken);
    }

    private static object BuildVendorRegisterBody(string email, string password = "P@ssword1234")
    {
        var unique = Guid.NewGuid().ToString("N");
        var phone = "018" + Random.Shared.Next(10000000, 99999999);
        return new
        {
            fullName = "Vendor Owner",
            email,
            phone,
            password,
            businessNameAr = "متجر الاختبار",
            businessNameEn = "Test Store",
            businessType = "Grocery",
            commercialRegistrationNumber = "REG" + unique[..8],
            commercialRegistrationExpiryDate = DateTime.UtcNow.AddYears(1),
            contactEmail = $"contact_{unique}@vendor.com",
            contactPhone = phone,
            descriptionAr = "وصف",
            descriptionEn = "Description",
            ownerName = "Vendor Owner",
            ownerEmail = email,
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
            branchAddressLine = "123 Main St",
            branchLatitude = 26.3927m,
            branchLongitude = 49.9777m,
            branchContactPhone = phone,
            branchDeliveryRadiusKm = 5.0m
        };
    }

    private sealed record CsrfTokenResponse(string CsrfToken);
}
