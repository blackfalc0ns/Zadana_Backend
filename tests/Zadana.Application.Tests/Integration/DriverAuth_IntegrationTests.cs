using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Tests.Helpers;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Tests.Integration;

/// <summary>
/// Integration tests for Driver registration and auth endpoints.
/// </summary>
public class DriverAuth_IntegrationTests : IClassFixture<ZadanaWebFactory>
{
    private readonly ZadanaWebFactory _factory;
    private readonly HttpClient _client;

    public DriverAuth_IntegrationTests(ZadanaWebFactory factory)
    {
        _factory = factory;
        _factory.OtpSink.Clear();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterDriver_WithValidData_Returns200WithoutTokens()
    {
        var email = $"driver_{Guid.NewGuid():N}@test.com";
        var body = BuildDriverRegisterBody(email);

        var response = await _client.PostAsJsonAsync("/api/drivers/register", body);

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
    public async Task RegisterDriver_WithMissingPhone_Returns400()
    {
        var body = new
        {
            fullName = "Incomplete Driver",
            email = "noPhone@test.com",
            password = "P@ssword1234"
        };

        var response = await _client.PostAsJsonAsync("/api/drivers/register", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DriverLogin_WithValidCredentials_ReturnsToken()
    {
        var email = $"dlogin_{Guid.NewGuid():N}@test.com";
        var password = "P@ssword1234";

        var registerResponse = await _client.PostAsJsonAsync(
            "/api/drivers/register",
            BuildDriverRegisterBody(email, password));
        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK, registerContent);

        var otpCode = _factory.OtpSink.EmailDispatches
            .Single(dispatch => dispatch.Recipient == email)
            .OtpCode;

        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/drivers/auth/verify-otp",
            new { identifier = email, otpCode });
        var verifyContent = await verifyResponse.Content.ReadAsStringAsync();
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK, verifyContent);

        var response = await _client.PostAsJsonAsync("/api/drivers/auth/login",
            new { identifier = email, password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("accessToken");
    }

    [Fact]
    public async Task GetDriverMe_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/drivers/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDriverStatus_AfterOtpVerification_ReturnsOperationalWorkflowState()
    {
        var email = $"driver_status_{Guid.NewGuid():N}@test.com";
        var registerResponse = await _client.PostAsJsonAsync(
            "/api/drivers/register",
            BuildDriverRegisterBody(email));

        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK, registerContent);

        var otpCode = _factory.OtpSink.EmailDispatches
            .Single(dispatch => dispatch.Recipient == email)
            .OtpCode;

        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/drivers/auth/verify-otp",
            new { identifier = email, otpCode });
        var verifyContent = await verifyResponse.Content.ReadAsStringAsync();
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK, verifyContent);

        using var authDocument = JsonDocument.Parse(verifyContent);
        authDocument.RootElement.GetProperty("tokens").GetProperty("accessToken").GetString()
            .Should().NotBeNullOrWhiteSpace();

        var driverStatus = authDocument.RootElement.GetProperty("driverStatus");
        driverStatus.GetProperty("isOperational").GetBoolean().Should().BeFalse();
        driverStatus.GetProperty("canReceiveOrders").GetBoolean().Should().BeFalse();
        driverStatus.GetProperty("canGoAvailable").GetBoolean().Should().BeFalse();
        driverStatus.GetProperty("verificationStatus").GetString().Should().Be("UnderReview");
        driverStatus.GetProperty("accountStatus").GetString().Should().Be("Pending");
    }

    [Fact]
    public async Task ResetPassword_WithValidOtp_UpdatesDriverPasswordAndAllowsLogin()
    {
        var email = $"driver_reset_{Guid.NewGuid():N}@test.com";
        var phone = "016" + Random.Shared.Next(10000000, 99999999);
        const string originalPassword = "P@ssword1234";
        const string newPassword = "Yahya123!";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var identityAccountService = scope.ServiceProvider.GetRequiredService<IIdentityAccountService>();

        var user = new User("Driver Reset Test", email, phone, UserRole.Driver);
        var createResult = await userManager.CreateAsync(user, originalPassword);
        createResult.Succeeded.Should().BeTrue(string.Join(", ", createResult.Errors.Select(error => error.Description)));
        await userManager.AddToRoleAsync(user, UserRole.Driver.ToString());
        user.VerifyEmail();
        await userManager.UpdateAsync(user);

        var otpResult = await identityAccountService.GeneratePasswordResetOtpAsync(email);
        otpResult.Status.Should().Be(OtpDispatchStatus.Succeeded);
        var otpCode = otpResult.OtpCode!;

        var verifyResponse = await _client.PostAsJsonAsync("/api/drivers/auth/verify-reset-otp", new
        {
            identifier = email,
            otpCode
        });
        var verifyContent = await verifyResponse.Content.ReadAsStringAsync();
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK, verifyContent);

        using var verifyDocument = JsonDocument.Parse(verifyContent);
        var resetToken = verifyDocument.RootElement.GetProperty("resetToken").GetString();
        resetToken.Should().NotBeNullOrWhiteSpace();

        var resetResponse = await _client.PostAsJsonAsync("/api/drivers/auth/reset-password", new
        {
            identifier = email,
            resetToken,
            newPassword
        });

        var resetContent = await resetResponse.Content.ReadAsStringAsync();
        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK, resetContent);

        var oldLoginResponse = await _client.PostAsJsonAsync("/api/drivers/auth/login", new
        {
            identifier = email,
            password = originalPassword
        });
        oldLoginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var newLoginResponse = await _client.PostAsJsonAsync("/api/drivers/auth/login", new
        {
            identifier = email,
            password = newPassword
        });
        var loginContent = await newLoginResponse.Content.ReadAsStringAsync();
        newLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK, loginContent);
    }

    private static object BuildDriverRegisterBody(string email, string password = "P@ssword1234")
    {
        var unique = Guid.NewGuid().ToString("N");
        return new
        {
            fullName = "Test Driver",
            email,
            phone = "019" + Random.Shared.Next(10000000, 99999999),
            password,
            vehicleType = "Motorcycle",
            nationalId = "2900101" + Random.Shared.Next(1000000, 9999999),
            licenseNumber = "LIC-" + unique[..6].ToUpperInvariant(),
            nationalIdExpiryDate = DateTime.UtcNow.AddYears(1),
            driverLicenseExpiryDate = DateTime.UtcNow.AddYears(1),
            vehicleLicenseNumber = "VL-" + unique[..6].ToUpperInvariant(),
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
    }
}
