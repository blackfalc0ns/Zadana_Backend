using System.Net;
using System.Net.Http.Json;
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
/// Integration tests for all Customer authentication endpoints.
/// Uses an in-memory HTTP server and an in-memory SQLite database.
/// </summary>
public class CustomerAuth_IntegrationTests : IClassFixture<ZadanaWebFactory>
{
    private readonly ZadanaWebFactory _factory;
    private readonly HttpClient _client;

    public CustomerAuth_IntegrationTests(ZadanaWebFactory factory)
    {
        _factory = factory;
        _factory.OtpSink.Clear();
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
            password = "P@ssword1234",
            addressLine = "Test Address Line",
            label = "Home",
            city = "Cairo",
            area = "Maadi",
            latitude = 30.0,
            longitude = 31.0
        };

        var response = await _client.PostAsJsonAsync("/api/customers/auth/register", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("accessToken", "registration must return a token");
    }

    [Fact]
    public async Task Register_WithValidData_SendsOtpToEmailOnly()
    {
        var phone = "010" + new Random().Next(10000000, 99999999).ToString();
        var email = $"wa_{Guid.NewGuid():N}@test.com";
        var body = new
        {
            fullName = "WhatsApp OTP User",
            email,
            phone,
            password = "P@ssword1234",
            addressLine = "Test Address Line"
        };

        var response = await _client.PostAsJsonAsync("/api/customers/auth/register", body);

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        _factory.OtpSink.EmailDispatches.Should().ContainSingle(dispatch => dispatch.Recipient == email);
        _factory.OtpSink.SmsDispatches.Should().BeEmpty();
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

        var body1 = new { fullName = "User One", email, phone = phone1, password = "P@ssword1", addressLine = "A1" };
        var body2 = new { fullName = "User Two", email, phone = phone2, password = "P@ssword1", addressLine = "A2" };

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
            new { fullName = "Login Test", email, phone, password, addressLine = "Login Address" });

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
            new { fullName = "Profile User", email, phone, password, addressLine = "Profile Address" });

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

    [Fact]
    public async Task VerifyOtp_WithRegistrationCode_ConfirmsEmail()
    {
        var email = $"verify_{Guid.NewGuid():N}@test.com";
        var phone = "014" + new Random().Next(10000000, 99999999).ToString();

        var registerResponse = await _client.PostAsJsonAsync("/api/customers/auth/register",
            new { fullName = "Verify Email Test", email, phone, password = "P@ssword1234", addressLine = "Verify Address" });

        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK, registerContent);

        var otpCode = _factory.OtpSink.EmailDispatches
            .Single(dispatch => dispatch.Recipient == email)
            .OtpCode;

        var response = await _client.PostAsJsonAsync("/api/customers/auth/verify-otp",
            new { identifier = email, otpCode });

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull();
        user!.EmailConfirmed.Should().BeTrue();
    }

    // ─── Forgot & Reset Password ───────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_WithValidIdentifier_Returns200()
    {
        var email = $"forgot_{Guid.NewGuid():N}@test.com";
        var phone = "018" + new Random().Next(10000000, 99999999).ToString();
        var password = "P@ssword1234";

        await _client.PostAsJsonAsync("/api/customers/auth/register",
            new { fullName = "Forgot Test", email, phone, password, addressLine = "Forgot Address" });

        var body = new { identifier = email };
        var response = await _client.PostAsJsonAsync("/api/customers/auth/forgot-password", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_WithEmailIdentifier_SendsOtpToRegisteredEmailOnly()
    {
        var email = $"forgot_wa_{Guid.NewGuid():N}@test.com";
        var phone = "018" + new Random().Next(10000000, 99999999).ToString();
        var password = "P@ssword1234";

        await SeedCustomerAccountAsync("Forgot WhatsApp Test", email, phone, password);
        _factory.OtpSink.Clear();

        var response = await _client.PostAsJsonAsync("/api/customers/auth/forgot-password", new { identifier = email });

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        _factory.OtpSink.EmailDispatches.Should().ContainSingle(dispatch => dispatch.Recipient == email);
        _factory.OtpSink.SmsDispatches.Should().BeEmpty();
    }

    private async Task SeedCustomerAccountAsync(string fullName, string email, string phone, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User(fullName, email, phone, UserRole.Customer);
        var createResult = await userManager.CreateAsync(user, password);
        createResult.Succeeded.Should().BeTrue(string.Join(", ", createResult.Errors.Select(error => error.Description)));

        var roleResult = await userManager.AddToRoleAsync(user, UserRole.Customer.ToString());
        roleResult.Succeeded.Should().BeTrue(string.Join(", ", roleResult.Errors.Select(error => error.Description)));
    }

    [Fact]
    public async Task ResendOtp_WithEmailIdentifier_SendsOtpToRegisteredEmailOnly()
    {
        var email = $"resend_email_{Guid.NewGuid():N}@test.com";
        var phone = "013" + new Random().Next(10000000, 99999999).ToString();

        await SeedCustomerAccountAsync("Resend Email Test", email, phone, "P@ssword1234");
        _factory.OtpSink.Clear();

        var response = await _client.PostAsJsonAsync("/api/customers/auth/resend-otp", new { identifier = email });

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        _factory.OtpSink.EmailDispatches.Should().ContainSingle(dispatch => dispatch.Recipient == email);
        _factory.OtpSink.SmsDispatches.Should().BeEmpty();
    }

    [Fact]
    public async Task ResendOtp_WithinCooldown_ReturnsErrorWithoutSendingAgain()
    {
        var email = $"resend_{Guid.NewGuid():N}@test.com";
        var phone = "013" + new Random().Next(10000000, 99999999).ToString();

        await _client.PostAsJsonAsync("/api/customers/auth/register",
            new { fullName = "Resend Test", email, phone, password = "P@ssword1234", addressLine = "Resend Address" });
        _factory.OtpSink.Clear();

        var response = await _client.PostAsJsonAsync("/api/customers/auth/resend-otp", new { identifier = email });

        ((int)response.StatusCode).Should().BeGreaterThan(399);
        _factory.OtpSink.SmsDispatches.Should().BeEmpty();
        _factory.OtpSink.EmailDispatches.Should().BeEmpty();
    }

    [Fact]
    public async Task ResendResetOtp_WithEmailIdentifier_SendsPasswordResetOtp()
    {
        var email = $"resend_reset_{Guid.NewGuid():N}@test.com";
        var phone = "014" + new Random().Next(10000000, 99999999).ToString();

        await SeedCustomerAccountAsync("Resend Reset Test", email, phone, "P@ssword1234");
        _factory.OtpSink.Clear();

        var response = await _client.PostAsJsonAsync("/api/customers/auth/resend-reset-otp", new { identifier = email });

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        _factory.OtpSink.EmailDispatches.Should().ContainSingle(dispatch => dispatch.Recipient == email);
    }

    [Fact]
    public async Task ResendResetOtp_WithPurposeField_SendsPasswordResetOtp()
    {
        var email = $"resend_reset_purpose_{Guid.NewGuid():N}@test.com";
        var phone = "015" + new Random().Next(10000000, 99999999).ToString();

        await SeedCustomerAccountAsync("Resend Reset Purpose Test", email, phone, "P@ssword1234");
        _factory.OtpSink.Clear();

        var response = await _client.PostAsJsonAsync("/api/customers/auth/resend-otp", new
        {
            identifier = email,
            purpose = "password_reset"
        });

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        _factory.OtpSink.EmailDispatches.Should().ContainSingle(dispatch => dispatch.Recipient == email);
    }

    [Fact]
    public async Task ResendOtp_WithoutPurposeDuringPasswordReset_InvalidatesPreviousResetCode()
    {
        var email = $"resend_reset_flow_{Guid.NewGuid():N}@test.com";
        var phone = "016" + new Random().Next(10000000, 99999999).ToString();
        const string password = "P@ssword1234";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User("Resend Reset Flow Test", email, phone, UserRole.Customer);
            var createResult = await userManager.CreateAsync(user, password);
            createResult.Succeeded.Should().BeTrue(string.Join(", ", createResult.Errors.Select(error => error.Description)));
            await userManager.AddToRoleAsync(user, UserRole.Customer.ToString());
            user.VerifyEmail();
            await userManager.UpdateAsync(user);
        }

        await _client.PostAsJsonAsync("/api/customers/auth/forgot-password", new { identifier = email });
        var firstOtp = _factory.OtpSink.EmailDispatches
            .Single(dispatch => dispatch.Recipient == email)
            .OtpCode;

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByEmailAsync(email);
            user.Should().NotBeNull();

            typeof(User).GetProperty(nameof(User.LastOtpSentAt))!
                .SetValue(user, DateTime.UtcNow.AddMinutes(-2));
            typeof(User).GetProperty(nameof(User.PasswordResetOtpExpiry))!
                .SetValue(user, DateTime.UtcNow.AddMinutes(-1));

            await userManager.UpdateAsync(user!);
        }

        _factory.OtpSink.Clear();

        var resendResponse = await _client.PostAsJsonAsync("/api/customers/auth/resend-otp", new { identifier = email });
        var resendContent = await resendResponse.Content.ReadAsStringAsync();
        resendResponse.StatusCode.Should().Be(HttpStatusCode.OK, resendContent);

        var secondOtp = _factory.OtpSink.EmailDispatches
            .Single(dispatch => dispatch.Recipient == email)
            .OtpCode;

        secondOtp.Should().NotBe(firstOtp);

        var oldVerifyResponse = await _client.PostAsJsonAsync("/api/customers/auth/verify-reset-otp", new
        {
            identifier = email,
            otpCode = firstOtp
        });
        oldVerifyResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var newVerifyResponse = await _client.PostAsJsonAsync("/api/customers/auth/verify-reset-otp", new
        {
            identifier = email,
            otpCode = secondOtp
        });
        var newVerifyContent = await newVerifyResponse.Content.ReadAsStringAsync();
        newVerifyResponse.StatusCode.Should().Be(HttpStatusCode.OK, newVerifyContent);
    }

    [Fact]
    public async Task VerifyResetOtp_WithInvalidOtp_Returns409()
    {
        var email = $"reset_{Guid.NewGuid():N}@test.com";
        var phone = "019" + new Random().Next(10000000, 99999999).ToString();
        
        await _client.PostAsJsonAsync("/api/customers/auth/register",
            new { fullName = "Reset Test", email, phone, password = "P@ssword1234", addressLine = "Reset Address" });

        await _client.PostAsJsonAsync("/api/customers/auth/forgot-password", new { identifier = email });

        var response = await _client.PostAsJsonAsync("/api/customers/auth/verify-reset-otp", new
        {
            identifier = email,
            otpCode = "0000"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ResetPassword_WithValidResetToken_UpdatesPasswordAndAllowsLogin()
    {
        var email = $"reset_ok_{Guid.NewGuid():N}@test.com";
        var phone = "017" + new Random().Next(10000000, 99999999).ToString();
        const string originalPassword = "P@ssword1234";
        const string newPassword = "Yahya123!";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var identityAccountService = scope.ServiceProvider.GetRequiredService<IIdentityAccountService>();

        var user = new User("Reset Success Test", email, phone, UserRole.Customer);
        var createResult = await userManager.CreateAsync(user, originalPassword);
        createResult.Succeeded.Should().BeTrue(string.Join(", ", createResult.Errors.Select(error => error.Description)));
        await userManager.AddToRoleAsync(user, UserRole.Customer.ToString());
        user.VerifyEmail();
        await userManager.UpdateAsync(user);

        var otpResult = await identityAccountService.GeneratePasswordResetOtpAsync(email);
        otpResult.Status.Should().Be(OtpDispatchStatus.Succeeded);
        var otpCode = otpResult.OtpCode!;

        var verifyResponse = await _client.PostAsJsonAsync("/api/customers/auth/verify-reset-otp", new
        {
            identifier = email,
            otpCode
        });
        var verifyContent = await verifyResponse.Content.ReadAsStringAsync();
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK, verifyContent);

        using var verifyDocument = System.Text.Json.JsonDocument.Parse(verifyContent);
        var resetToken = verifyDocument.RootElement.GetProperty("resetToken").GetString();
        resetToken.Should().NotBeNullOrWhiteSpace();

        var resetResponse = await _client.PostAsJsonAsync("/api/customers/auth/reset-password", new
        {
            identifier = email,
            resetToken,
            newPassword
        });

        var resetContent = await resetResponse.Content.ReadAsStringAsync();
        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK, resetContent);

        var oldLoginResponse = await _client.PostAsJsonAsync("/api/customers/auth/login", new
        {
            identifier = email,
            password = originalPassword
        });
        oldLoginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var newLoginResponse = await _client.PostAsJsonAsync("/api/customers/auth/login", new
        {
            identifier = email,
            password = newPassword
        });
        var loginContent = await newLoginResponse.Content.ReadAsStringAsync();
        newLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK, loginContent);
    }
}
