using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zadana.Application.Tests.Helpers;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Application.Tests.Integration;

/// <summary>
/// End-to-end coverage for deferring AspNetUsers creation until OTP succeeds (signed registration token).
/// </summary>
public class PendingRegistrationFlow_IntegrationTests : IClassFixture<ZadanaWebFactory>
{
    private readonly ZadanaWebFactory _factory;
    private readonly HttpClient _client;

    public PendingRegistrationFlow_IntegrationTests(ZadanaWebFactory factory)
    {
        _factory = factory;
        _factory.OtpSink.Clear();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CustomerRegister_DoesNotCreateUser_UntilOtpSucceeds()
    {
        var email = $"pending_cust_{Guid.NewGuid():N}@test.com";
        var phone = "010" + Random.Shared.Next(10000000, 99999999);

        var registerResponse = await _client.PostAsJsonAsync("/api/customers/auth/register", new
        {
            fullName = "Pending Customer",
            email,
            phone,
            password = "P@ssword1234",
            addressLine = "Test Address"
        });

        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK, registerContent);

        using var registerDoc = JsonDocument.Parse(registerContent);
        var registrationToken = registerDoc.RootElement.GetProperty("registrationToken").GetString();
        registrationToken.Should().NotBeNullOrWhiteSpace();
        registerDoc.RootElement.GetProperty("isVerified").GetBoolean().Should().BeFalse();

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            (await userManager.FindByEmailAsync(email)).Should().BeNull();
        }

        var wrongOtpResponse = await _client.PostAsJsonAsync("/api/customers/auth/verify-otp",
            new { identifier = email, otpCode = "0000", registrationToken });
        ((int)wrongOtpResponse.StatusCode).Should().BeGreaterThan(399);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            (await userManager.FindByEmailAsync(email)).Should().BeNull();
        }

        var otpCode = _factory.OtpSink.EmailDispatches.Single(d => d.Recipient == email).OtpCode;
        var verifyResponse = await _client.PostAsJsonAsync("/api/customers/auth/verify-otp",
            new { identifier = email, otpCode, registrationToken });
        var verifyContent = await verifyResponse.Content.ReadAsStringAsync();
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK, verifyContent);

        using var doc = JsonDocument.Parse(verifyContent);
        doc.RootElement.GetProperty("isVerified").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("tokens").GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await userManager.FindByEmailAsync(email);
            user.Should().NotBeNull();
            user!.EmailConfirmed.Should().BeTrue();
            (await db.CustomerAddresses.AnyAsync(a => a.UserId == user.Id)).Should().BeTrue();
        }
    }

    [Fact]
    public async Task CustomerResendOtp_RequiresRegistrationToken_AndEnforcesCooldown()
    {
        var email = $"resend_cust_{Guid.NewGuid():N}@test.com";
        var phone = "010" + Random.Shared.Next(10000000, 99999999);

        var registerResponse = await _client.PostAsJsonAsync("/api/customers/auth/register", new
        {
            fullName = "Resend Customer",
            email,
            phone,
            password = "P@ssword1234",
            addressLine = "Test Address"
        });
        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK, registerContent);

        using var registerDoc = JsonDocument.Parse(registerContent);
        var registrationToken = registerDoc.RootElement.GetProperty("registrationToken").GetString();
        registrationToken.Should().NotBeNullOrWhiteSpace();

        var missingTokenResponse = await _client.PostAsJsonAsync("/api/customers/auth/resend-otp",
            new { identifier = email });
        ((int)missingTokenResponse.StatusCode).Should().BeGreaterThan(399);

        var cooldownResponse = await _client.PostAsJsonAsync("/api/customers/auth/resend-otp",
            new { identifier = email, registrationToken });
        var cooldownContent = await cooldownResponse.Content.ReadAsStringAsync();
        ((int)cooldownResponse.StatusCode).Should().BeGreaterThan(399);
        cooldownContent.Should().ContainEquivalentOf("OTP_COOLDOWN");
    }

    [Fact]
    public async Task CustomerRegister_WhenEmailAlreadyInUsers_ReturnsUserAlreadyExists()
    {
        var email = $"exists_cust_{Guid.NewGuid():N}@test.com";
        var phone = "010" + Random.Shared.Next(10000000, 99999999);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User("Existing", email, phone, Domain.Modules.Identity.Enums.UserRole.Customer);
            (await userManager.CreateAsync(user, "P@ssword1234")).Succeeded.Should().BeTrue();
        }

        var response = await _client.PostAsJsonAsync("/api/customers/auth/register", new
        {
            fullName = "Clash Customer",
            email,
            phone = "010" + Random.Shared.Next(10000000, 99999999),
            password = "P@ssword1234",
            addressLine = "Test Address"
        });

        ((int)response.StatusCode).Should().BeGreaterThan(399);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().ContainEquivalentOf("USER_ALREADY_EXISTS");
    }

    [Fact]
    public async Task CustomerLogin_BeforeOtp_TreatsAsAccountNotFound()
    {
        var email = $"ghost_cust_{Guid.NewGuid():N}@test.com";
        var phone = "010" + Random.Shared.Next(10000000, 99999999);
        var password = "P@ssword1234";

        var registerResponse = await _client.PostAsJsonAsync("/api/customers/auth/register", new
        {
            fullName = "Ghost Customer",
            email,
            phone,
            password,
            addressLine = "Test Address"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await _client.PostAsJsonAsync("/api/customers/auth/login", new
        {
            identifier = email,
            password
        });

        ((int)loginResponse.StatusCode).Should().BeOneOf(401, 403, 404);
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            (await userManager.FindByEmailAsync(email)).Should().BeNull();
        }
    }
}
