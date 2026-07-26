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
/// End-to-end coverage for deferring AspNetUsers creation until OTP succeeds.
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

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await userManager.FindByEmailAsync(email)).Should().BeNull();
            (await db.PendingRegistrations.AnyAsync(x => x.Email == email.ToLowerInvariant())).Should().BeTrue();
        }

        var wrongOtpResponse = await _client.PostAsJsonAsync("/api/customers/auth/verify-otp",
            new { identifier = email, otpCode = "0000" });
        ((int)wrongOtpResponse.StatusCode).Should().BeGreaterThan(399);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            (await userManager.FindByEmailAsync(email)).Should().BeNull();
        }

        var otpCode = _factory.OtpSink.EmailDispatches.Single(d => d.Recipient == email).OtpCode;
        var verifyResponse = await _client.PostAsJsonAsync("/api/customers/auth/verify-otp",
            new { identifier = email, otpCode });
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
            (await db.PendingRegistrations.AnyAsync(x => x.Email == email.ToLowerInvariant())).Should().BeFalse();
            (await db.CustomerAddresses.AnyAsync(a => a.UserId == user.Id)).Should().BeTrue();
        }
    }

    [Fact]
    public async Task CustomerResendOtp_WorksOnPendingRegistration()
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
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstCode = _factory.OtpSink.EmailDispatches.Single(d => d.Recipient == email).OtpCode;
        _factory.OtpSink.Clear();

        // Advance past cooldown by backdating LastOtpSentAtUtc.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pending = await db.PendingRegistrations.SingleAsync(x => x.Email == email.ToLowerInvariant());
            db.Entry(pending).Property(nameof(PendingRegistration.LastOtpSentAtUtc))
                .CurrentValue = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }

        var resendResponse = await _client.PostAsJsonAsync("/api/customers/auth/resend-otp",
            new { identifier = email });
        var resendContent = await resendResponse.Content.ReadAsStringAsync();
        resendResponse.StatusCode.Should().Be(HttpStatusCode.OK, resendContent);

        var resentCode = _factory.OtpSink.EmailDispatches.Should().ContainSingle(d => d.Recipient == email).Subject.OtpCode;
        resentCode.Should().NotBeNullOrWhiteSpace();

        var verifyResponse = await _client.PostAsJsonAsync("/api/customers/auth/verify-otp",
            new { identifier = email, otpCode = resentCode });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Old code from first dispatch must not create a second user after success.
        firstCode.Should().NotBe(resentCode);
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
}
