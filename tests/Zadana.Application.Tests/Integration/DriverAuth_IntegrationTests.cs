using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Zadana.Application.Tests.Helpers;

namespace Zadana.Application.Tests.Integration;

/// <summary>
/// Integration tests for Driver registration and auth endpoints.
/// </summary>
public class DriverAuth_IntegrationTests : IClassFixture<ZadanaWebFactory>
{
    private readonly HttpClient _client;

    public DriverAuth_IntegrationTests(ZadanaWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterDriver_WithValidData_Returns200()
    {
        var body = new
        {
            fullName = "Test Driver",
            email = $"driver_{Guid.NewGuid():N}@test.com",
            phone = "019" + new Random().Next(10000000, 99999999).ToString(),
            password = "P@ssword1234",
            vehicleType = "Motorcycle",
            nationalId = "2900101" + new Random().Next(1000000, 9999999).ToString(),
            licenseNumber = "LIC-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            nationalIdImageUrl = "http://test.com/img1.jpg",
            licenseImageUrl = "http://test.com/img2.jpg",
            vehicleImageUrl = "http://test.com/img3.jpg",
            personalPhotoUrl = "http://test.com/img4.jpg"
        };

        var response = await _client.PostAsJsonAsync("/api/drivers/register", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("accessToken", "driver registration must return tokens");
    }

    [Fact]
    public async Task RegisterDriver_WithMissingPhone_Returns400()
    {
        var body = new
        {
            fullName = "Incomplete Driver",
            email = "noPhone@test.com",
            password = "P@ssword1234"
            // phone missing
        };

        var response = await _client.PostAsJsonAsync("/api/drivers/register", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DriverLogin_WithValidCredentials_ReturnsToken()
    {
        // First register
        var email = $"dlogin_{Guid.NewGuid():N}@test.com";
        var phone = "0111" + new Random().Next(1000000, 9999999).ToString();
        var password = "P@ssword1234";

        await _client.PostAsJsonAsync("/api/drivers/register", new
        {
            fullName = "Login Driver",
            email,
            phone,
            password,
            vehicleType = "Car",
            nationalId = "2900101" + new Random().Next(1000000, 9999999).ToString(),
            licenseNumber = "LIC-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            nationalIdImageUrl = "http://test.com/img1.jpg",
            licenseImageUrl = "http://test.com/img2.jpg",
            vehicleImageUrl = "http://test.com/img3.jpg",
            personalPhotoUrl = "http://test.com/img4.jpg"
        });

        // Then login
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
    public async Task GetDriverStatus_AfterRegistration_ReturnsOperationalWorkflowState()
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/drivers/register", new
        {
            fullName = "Workflow Driver",
            email = $"driver_status_{Guid.NewGuid():N}@test.com",
            phone = "012" + new Random().Next(10000000, 99999999).ToString(),
            password = "P@ssword1234",
            vehicleType = "Motorcycle",
            nationalId = "2900101" + new Random().Next(1000000, 9999999).ToString(),
            licenseNumber = "LIC-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            nationalIdImageUrl = "http://test.com/img1.jpg",
            licenseImageUrl = "http://test.com/img2.jpg",
            vehicleImageUrl = "http://test.com/img3.jpg",
            personalPhotoUrl = "http://test.com/img4.jpg"
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var authDocument = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        var accessToken = authDocument.RootElement
            .GetProperty("tokens")
            .GetProperty("accessToken")
            .GetString();

        accessToken.Should().NotBeNullOrWhiteSpace();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/drivers/me/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var statusResponse = await _client.SendAsync(request);

        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var statusDocument = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
        statusDocument.RootElement.GetProperty("isOperational").GetBoolean().Should().BeFalse();
        statusDocument.RootElement.GetProperty("canReceiveOrders").GetBoolean().Should().BeFalse();
        statusDocument.RootElement.GetProperty("canGoAvailable").GetBoolean().Should().BeFalse();
        statusDocument.RootElement.GetProperty("verificationStatus").GetString().Should().Be("UnderReview");
        statusDocument.RootElement.GetProperty("accountStatus").GetString().Should().Be("Pending");
    }
}
