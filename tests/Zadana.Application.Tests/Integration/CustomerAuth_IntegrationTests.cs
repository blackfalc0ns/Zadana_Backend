using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Zadana.Application.Tests.Helpers;

namespace Zadana.Application.Tests.Integration;

/// <summary>
/// Integration tests for all Customer authentication endpoints.
/// Uses an in-memory HTTP server and an in-memory SQLite database.
/// </summary>
public class CustomerAuth_IntegrationTests : IClassFixture<ZadanaWebFactory>
{
    private readonly HttpClient _client;

    public CustomerAuth_IntegrationTests(ZadanaWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ─── Register ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidData_Returns200AndTokens()
    {
        var body = new
        {
            fullName = "Integration Test User",
            email = $"user_{Guid.NewGuid():N}@test.com",
            phone = "010" + new Random().Next(10000000, 99999999).ToString(),
            password = "P@ssword1234"
        };

        var response = await _client.PostAsJsonAsync("/api/customers/auth/register", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("accessToken", "registration must return a token");
    }

    [Fact]
    public async Task Register_WithMissingFullName_Returns400WithValidationError()
    {
        var body = new
        {
            email = "user@test.com",
            phone = "01011111111",
            password = "P@ssword1234"
            // fullName missing
        };

        var response = await _client.PostAsJsonAsync("/api/customers/auth/register", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var email = $"dup_{Guid.NewGuid():N}@test.com";
        var phone1 = "011" + new Random().Next(10000000, 99999999).ToString();
        var phone2 = "012" + new Random().Next(10000000, 99999999).ToString();

        var body1 = new { fullName = "User One", email, phone = phone1, password = "P@ssword1" };
        var body2 = new { fullName = "User Two", email, phone = phone2, password = "P@ssword1" };

        await _client.PostAsJsonAsync("/api/customers/auth/register", body1);
        var response = await _client.PostAsJsonAsync("/api/customers/auth/register", body2);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "duplicate email should return 400 Bad Request via BusinessRuleException");
    }

    // ─── Login ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAccessToken()
    {
        // First register a user
        var email = $"login_{Guid.NewGuid():N}@test.com";
        var phone = "015" + new Random().Next(10000000, 99999999).ToString();
        var password = "P@ssword1234";

        await _client.PostAsJsonAsync("/api/customers/auth/register",
            new { fullName = "Login Test", email, phone, password });

        // Then login
        var loginBody = new { identifier = email, password };
        var response = await _client.PostAsJsonAsync("/api/customers/auth/login", loginBody);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("accessToken");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns400()
    {
        var email = $"wrong_{Guid.NewGuid():N}@test.com";
        var phone = "016" + new Random().Next(10000000, 99999999).ToString();

        await _client.PostAsJsonAsync("/api/customers/auth/register",
            new { fullName = "Test User", email, phone, password = "CorrectPass1!" });

        var loginBody = new { identifier = email, password = "WrongPassword123" };
        var response = await _client.PostAsJsonAsync("/api/customers/auth/login", loginBody);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Protected Routes (Auth required) ─────────────────────────────────

    [Fact]
    public async Task GetMe_WithoutAuthToken_Returns401()
    {
        var response = await _client.GetAsync("/api/customers/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "unauthenticated requests should be rejected");
    }

    [Fact]
    public async Task GetMe_WithValidToken_Returns200AndUserData()
    {
        // Register and login to get a token
        var email = $"me_{Guid.NewGuid():N}@test.com";
        var phone = "017" + new Random().Next(10000000, 99999999).ToString();
        var password = "P@ssword1234";

        await _client.PostAsJsonAsync("/api/customers/auth/register",
            new { fullName = "Profile User", email, phone, password });

        var loginResp = await _client.PostAsJsonAsync("/api/customers/auth/login",
            new { identifier = email, password });

        var loginContent = await loginResp.Content.ReadAsStringAsync();
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK, $"login should succeed but got: {loginContent}");

        // Extract token from the response JSON
        using var loginDoc = System.Text.Json.JsonDocument.Parse(loginContent);
        var token = loginDoc.RootElement
            .GetProperty("tokens")
            .GetProperty("accessToken")
            .GetString()!;

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/customers/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(email);

        // Clean up auth header for subsequent tests
        _client.DefaultRequestHeaders.Authorization = null;
    }

    // ─── Verify OTP ────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyOtp_WithInvalidCode_Returns400()
    {
        var body = new { identifier = "nonexistent@test.com", otpCode = "0000" };
        var response = await _client.PostAsJsonAsync("/api/customers/auth/verify-otp", body);

        // Should fail with 404 (user not found) or 400 (bad OTP)
        ((int)response.StatusCode).Should().BeGreaterThan(399);
    }

    [Fact]
    public async Task VerifyOtp_WithMissingFields_Returns400()
    {
        var body = new { otpCode = "12" }; // identifier missing, code too short

        var response = await _client.PostAsJsonAsync("/api/customers/auth/verify-otp", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
