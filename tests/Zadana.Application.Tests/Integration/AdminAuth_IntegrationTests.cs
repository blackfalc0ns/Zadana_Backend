using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Zadana.Application.Tests.Helpers;

namespace Zadana.Application.Tests.Integration;

/// <summary>
/// Integration tests for Admin authentication and Vendor Approval endpoints.
/// Uses the seeded SuperAdmin (admin@test.com / Admin@123) from ZadanaWebFactory.
/// </summary>
public class AdminAuth_IntegrationTests : IClassFixture<ZadanaWebFactory>
{
    private readonly HttpClient _client;

    public AdminAuth_IntegrationTests(ZadanaWebFactory factory)
    {
        factory.SeedTestData();
        _client = factory.CreateClient();
    }

    // ─── Admin Login ───────────────────────────────────────────────────────

    [Fact]
    public async Task AdminLogin_WithValidCredentials_ReturnsToken()
    {
        var body = new { identifier = "admin@test.com", password = "Admin@123" };
        var response = await _client.PostAsJsonAsync("/api/admin/auth/login", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("accessToken", "admin login must return a token");
    }

    [Fact]
    public async Task AdminLogin_WithWrongPassword_Returns401()
    {
        var body = new { identifier = "admin@test.com", password = "WrongPassword" };
        var response = await _client.PostAsJsonAsync("/api/admin/auth/login", body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAdminMe_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/admin/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Vendor Approval (Protected) ──────────────────────────────────────

    [Fact]
    public async Task ApproveVendor_WithoutToken_Returns401()
    {
        var fakeVendorId = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync(
            $"/api/admin/vendors/{fakeVendorId}/approve",
            new { commissionRate = 5.0 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "admin routes must require authentication");
    }

    [Fact]
    public async Task RejectVendor_WithoutToken_Returns401()
    {
        var fakeVendorId = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync(
            $"/api/admin/vendors/{fakeVendorId}/reject",
            new { reason = "Incomplete documents" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
