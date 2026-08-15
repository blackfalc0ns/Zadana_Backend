using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zadana.Application.Tests.Helpers;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Application.Tests.Integration;

public class CrossPlatformAuth_IntegrationTests : IClassFixture<ZadanaWebFactory>
{
    private readonly ZadanaWebFactory _factory;
    private readonly HttpClient _client;

    public CrossPlatformAuth_IntegrationTests(ZadanaWebFactory factory)
    {
        _factory = factory;
        _factory.OtpSink.Clear();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task VendorCanRegisterAsCustomer_WithSameEmail_AndLoginBothApps()
    {
        await EnsureVendorCsrfAsync();
        var email = $"multi_{Guid.NewGuid():N}@test.com";
        const string password = "P@ssword1234";

        var vendorRegister = await _client.PostAsJsonAsync(
            "/api/vendors/register",
            BuildVendorRegisterBody(email, password));
        var vendorRegisterContent = await vendorRegister.Content.ReadAsStringAsync();
        vendorRegister.StatusCode.Should().Be(HttpStatusCode.OK, vendorRegisterContent);
        var vendorToken = JsonDocument.Parse(vendorRegisterContent).RootElement.GetProperty("registrationToken").GetString();
        var vendorOtp = _factory.OtpSink.EmailDispatches.Single(d => d.Recipient == email).OtpCode;
        var vendorVerify = await _client.PostAsJsonAsync(
            "/api/vendors/auth/verify-otp",
            new { identifier = email, otpCode = vendorOtp, registrationToken = vendorToken });
        vendorVerify.StatusCode.Should().Be(HttpStatusCode.OK, await vendorVerify.Content.ReadAsStringAsync());

        var customerLoginBefore = await _client.PostAsJsonAsync(
            "/api/customers/auth/login",
            new { identifier = email, password });
        ((int)customerLoginBefore.StatusCode).Should().BeOneOf(401, 403, 404);

        _factory.OtpSink.Clear();
        var customerRegister = await _client.PostAsJsonAsync("/api/customers/auth/register", new
        {
            fullName = "Same Person",
            email,
            phone = "010" + Random.Shared.Next(10000000, 99999999),
            password,
            addressLine = "Customer address"
        });
        var customerRegisterContent = await customerRegister.Content.ReadAsStringAsync();
        customerRegister.StatusCode.Should().Be(HttpStatusCode.OK, customerRegisterContent);

        var customerToken = JsonDocument.Parse(customerRegisterContent).RootElement.GetProperty("registrationToken").GetString();
        var customerOtp = _factory.OtpSink.EmailDispatches.Single(d => d.Recipient == email).OtpCode;
        var customerVerify = await _client.PostAsJsonAsync(
            "/api/customers/auth/verify-otp",
            new { identifier = email, otpCode = customerOtp, registrationToken = customerToken });
        var customerVerifyContent = await customerVerify.Content.ReadAsStringAsync();
        customerVerify.StatusCode.Should().Be(HttpStatusCode.OK, customerVerifyContent);

        using var customerDoc = JsonDocument.Parse(customerVerifyContent);
        customerDoc.RootElement.GetProperty("user").GetProperty("role").GetString().Should().Be("Customer");

        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await userManager.FindByEmailAsync(email);
            user.Should().NotBeNull();
            userId = user!.Id;
            user.Role.Should().Be(UserRole.Vendor);
            var roles = await userManager.GetRolesAsync(user);
            roles.Should().Contain(UserRole.Vendor.ToString());
            roles.Should().Contain(UserRole.Customer.ToString());
            (await db.Vendors.CountAsync(v => v.UserId == user.Id)).Should().Be(1);
            (await db.CustomerAddresses.AnyAsync(a => a.UserId == user.Id)).Should().BeTrue();
        }

        var vendorLogin = await _client.PostAsJsonAsync(
            "/api/vendors/auth/login",
            new { identifier = email, password });
        var vendorLoginContent = await vendorLogin.Content.ReadAsStringAsync();
        vendorLogin.StatusCode.Should().Be(HttpStatusCode.OK, vendorLoginContent);
        using var vendorLoginDoc = JsonDocument.Parse(vendorLoginContent);
        vendorLoginDoc.RootElement.GetProperty("user").GetProperty("role").GetString().Should().Be("Vendor");
        vendorLoginDoc.RootElement.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);

        var customerLogin = await _client.PostAsJsonAsync(
            "/api/customers/auth/login",
            new { identifier = email, password });
        var customerLoginContent = await customerLogin.Content.ReadAsStringAsync();
        customerLogin.StatusCode.Should().Be(HttpStatusCode.OK, customerLoginContent);
        using var customerLoginDoc = JsonDocument.Parse(customerLoginContent);
        customerLoginDoc.RootElement.GetProperty("user").GetProperty("role").GetString().Should().Be("Customer");
        customerLoginDoc.RootElement.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);
    }

    [Fact]
    public async Task DriverCanRegisterAsCustomer_WithSamePhone_DifferentEmail()
    {
        var driverEmail = $"driver_link_{Guid.NewGuid():N}@test.com";
        var customerEmail = $"cust_link_{Guid.NewGuid():N}@test.com";
        var phone = "019" + Random.Shared.Next(10000000, 99999999);
        const string password = "P@ssword1234";

        var driverRegister = await _client.PostAsJsonAsync(
            "/api/drivers/register",
            BuildDriverRegisterBody(driverEmail, phone, password));
        var driverRegisterContent = await driverRegister.Content.ReadAsStringAsync();
        driverRegister.StatusCode.Should().Be(HttpStatusCode.OK, driverRegisterContent);
        var driverToken = JsonDocument.Parse(driverRegisterContent).RootElement.GetProperty("registrationToken").GetString();
        var driverOtp = _factory.OtpSink.EmailDispatches.Single(d => d.Recipient == driverEmail).OtpCode;
        var driverVerify = await _client.PostAsJsonAsync(
            "/api/drivers/auth/verify-otp",
            new { identifier = driverEmail, otpCode = driverOtp, registrationToken = driverToken });
        driverVerify.StatusCode.Should().Be(HttpStatusCode.OK, await driverVerify.Content.ReadAsStringAsync());

        _factory.OtpSink.Clear();
        var customerRegister = await _client.PostAsJsonAsync("/api/customers/auth/register", new
        {
            fullName = "Same Person Customer",
            email = customerEmail,
            phone,
            password,
            addressLine = "Customer address"
        });
        var customerRegisterContent = await customerRegister.Content.ReadAsStringAsync();
        customerRegister.StatusCode.Should().Be(HttpStatusCode.OK, customerRegisterContent);

        var customerToken = JsonDocument.Parse(customerRegisterContent).RootElement.GetProperty("registrationToken").GetString();
        _factory.OtpSink.EmailDispatches.Should().ContainSingle(d => d.Recipient == customerEmail);
        _factory.OtpSink.EmailDispatches.Should().NotContain(d => d.Recipient == driverEmail);
        var customerOtp = _factory.OtpSink.EmailDispatches.Single(d => d.Recipient == customerEmail).OtpCode;
        var customerVerify = await _client.PostAsJsonAsync(
            "/api/customers/auth/verify-otp",
            new { identifier = customerEmail, otpCode = customerOtp, registrationToken = customerToken });
        var customerVerifyContent = await customerVerify.Content.ReadAsStringAsync();
        customerVerify.StatusCode.Should().Be(HttpStatusCode.OK, customerVerifyContent);

        using var customerDoc = JsonDocument.Parse(customerVerifyContent);
        customerDoc.RootElement.GetProperty("user").GetProperty("role").GetString().Should().Be("Customer");
        customerDoc.RootElement.GetProperty("user").GetProperty("email").GetString().Should().Be(driverEmail);

        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await userManager.FindByEmailAsync(driverEmail);
            user.Should().NotBeNull();
            userId = user!.Id;
            (await userManager.FindByEmailAsync(customerEmail)).Should().BeNull();
            var roles = await userManager.GetRolesAsync(user);
            roles.Should().Contain(UserRole.Driver.ToString());
            roles.Should().Contain(UserRole.Customer.ToString());
            (await db.Drivers.CountAsync(d => d.UserId == user.Id)).Should().Be(1);
            (await db.CustomerAddresses.AnyAsync(a => a.UserId == user.Id)).Should().BeTrue();
        }

        var driverLogin = await _client.PostAsJsonAsync(
            "/api/drivers/auth/login",
            new { identifier = phone, password });
        driverLogin.StatusCode.Should().Be(HttpStatusCode.OK, await driverLogin.Content.ReadAsStringAsync());

        var customerLogin = await _client.PostAsJsonAsync(
            "/api/customers/auth/login",
            new { identifier = phone, password });
        var customerLoginContent = await customerLogin.Content.ReadAsStringAsync();
        customerLogin.StatusCode.Should().Be(HttpStatusCode.OK, customerLoginContent);
        using var customerLoginDoc = JsonDocument.Parse(customerLoginContent);
        customerLoginDoc.RootElement.GetProperty("user").GetProperty("role").GetString().Should().Be("Customer");
        customerLoginDoc.RootElement.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);
    }

    [Fact]
    public async Task CustomerCanRegisterAsDriver_WithSamePhone_DifferentEmail()
    {
        var customerEmail = $"cust_drv_{Guid.NewGuid():N}@test.com";
        var driverEmail = $"drv_from_cust_{Guid.NewGuid():N}@test.com";
        var phone = "019" + Random.Shared.Next(10000000, 99999999);
        const string password = "P@ssword1234";

        await RegisterAndVerifyCustomerAsync(customerEmail, phone, password);

        _factory.OtpSink.Clear();
        var driverRegister = await _client.PostAsJsonAsync(
            "/api/drivers/register",
            BuildDriverRegisterBody(driverEmail, phone, password));
        var driverRegisterContent = await driverRegister.Content.ReadAsStringAsync();
        driverRegister.StatusCode.Should().Be(HttpStatusCode.OK, driverRegisterContent);

        var driverToken = JsonDocument.Parse(driverRegisterContent).RootElement.GetProperty("registrationToken").GetString();
        _factory.OtpSink.EmailDispatches.Should().ContainSingle(d => d.Recipient == driverEmail);
        _factory.OtpSink.EmailDispatches.Should().NotContain(d => d.Recipient == customerEmail);
        var driverOtp = _factory.OtpSink.EmailDispatches.Single(d => d.Recipient == driverEmail).OtpCode;
        var driverVerify = await _client.PostAsJsonAsync(
            "/api/drivers/auth/verify-otp",
            new { identifier = driverEmail, otpCode = driverOtp, registrationToken = driverToken });
        var driverVerifyContent = await driverVerify.Content.ReadAsStringAsync();
        driverVerify.StatusCode.Should().Be(HttpStatusCode.OK, driverVerifyContent);

        using var driverDoc = JsonDocument.Parse(driverVerifyContent);
        driverDoc.RootElement.GetProperty("user").GetProperty("role").GetString().Should().Be("Driver");
        driverDoc.RootElement.GetProperty("user").GetProperty("email").GetString().Should().Be(customerEmail);

        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await userManager.FindByEmailAsync(customerEmail);
            user.Should().NotBeNull();
            userId = user!.Id;
            (await userManager.FindByEmailAsync(driverEmail)).Should().BeNull();
            var roles = await userManager.GetRolesAsync(user);
            roles.Should().Contain(UserRole.Customer.ToString());
            roles.Should().Contain(UserRole.Driver.ToString());
            (await db.Drivers.CountAsync(d => d.UserId == user.Id)).Should().Be(1);
            (await db.CustomerAddresses.AnyAsync(a => a.UserId == user.Id)).Should().BeTrue();
        }

        var customerLogin = await _client.PostAsJsonAsync(
            "/api/customers/auth/login",
            new { identifier = phone, password });
        customerLogin.StatusCode.Should().Be(HttpStatusCode.OK, await customerLogin.Content.ReadAsStringAsync());

        var driverLogin = await _client.PostAsJsonAsync(
            "/api/drivers/auth/login",
            new { identifier = phone, password });
        var driverLoginContent = await driverLogin.Content.ReadAsStringAsync();
        driverLogin.StatusCode.Should().Be(HttpStatusCode.OK, driverLoginContent);
        using var driverLoginDoc = JsonDocument.Parse(driverLoginContent);
        driverLoginDoc.RootElement.GetProperty("user").GetProperty("role").GetString().Should().Be("Driver");
        driverLoginDoc.RootElement.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);
    }

    [Fact]
    public async Task CustomerCanRegisterAsVendor_WithSamePhone_DifferentEmail()
    {
        await EnsureVendorCsrfAsync();
        var customerEmail = $"cust_vnd_{Guid.NewGuid():N}@test.com";
        var vendorEmail = $"vnd_from_cust_{Guid.NewGuid():N}@test.com";
        var phone = "018" + Random.Shared.Next(10000000, 99999999);
        const string password = "P@ssword1234";

        await RegisterAndVerifyCustomerAsync(customerEmail, phone, password);

        _factory.OtpSink.Clear();
        var vendorRegister = await _client.PostAsJsonAsync(
            "/api/vendors/register",
            BuildVendorRegisterBody(vendorEmail, password, phone));
        var vendorRegisterContent = await vendorRegister.Content.ReadAsStringAsync();
        vendorRegister.StatusCode.Should().Be(HttpStatusCode.OK, vendorRegisterContent);

        var vendorToken = JsonDocument.Parse(vendorRegisterContent).RootElement.GetProperty("registrationToken").GetString();
        _factory.OtpSink.EmailDispatches.Should().ContainSingle(d => d.Recipient == vendorEmail);
        _factory.OtpSink.EmailDispatches.Should().NotContain(d => d.Recipient == customerEmail);
        var vendorOtp = _factory.OtpSink.EmailDispatches.Single(d => d.Recipient == vendorEmail).OtpCode;
        var vendorVerify = await _client.PostAsJsonAsync(
            "/api/vendors/auth/verify-otp",
            new { identifier = vendorEmail, otpCode = vendorOtp, registrationToken = vendorToken });
        var vendorVerifyContent = await vendorVerify.Content.ReadAsStringAsync();
        vendorVerify.StatusCode.Should().Be(HttpStatusCode.OK, vendorVerifyContent);

        using var vendorDoc = JsonDocument.Parse(vendorVerifyContent);
        vendorDoc.RootElement.GetProperty("user").GetProperty("role").GetString().Should().Be("Vendor");
        vendorDoc.RootElement.GetProperty("user").GetProperty("email").GetString().Should().Be(customerEmail);

        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await userManager.FindByEmailAsync(customerEmail);
            user.Should().NotBeNull();
            userId = user!.Id;
            (await userManager.FindByEmailAsync(vendorEmail)).Should().BeNull();
            var roles = await userManager.GetRolesAsync(user);
            roles.Should().Contain(UserRole.Customer.ToString());
            roles.Should().Contain(UserRole.Vendor.ToString());
            (await db.Vendors.CountAsync(v => v.UserId == user.Id)).Should().Be(1);
            (await db.CustomerAddresses.AnyAsync(a => a.UserId == user.Id)).Should().BeTrue();
        }

        var customerLogin = await _client.PostAsJsonAsync(
            "/api/customers/auth/login",
            new { identifier = phone, password });
        customerLogin.StatusCode.Should().Be(HttpStatusCode.OK, await customerLogin.Content.ReadAsStringAsync());

        var vendorLogin = await _client.PostAsJsonAsync(
            "/api/vendors/auth/login",
            new { identifier = phone, password });
        var vendorLoginContent = await vendorLogin.Content.ReadAsStringAsync();
        vendorLogin.StatusCode.Should().Be(HttpStatusCode.OK, vendorLoginContent);
        using var vendorLoginDoc = JsonDocument.Parse(vendorLoginContent);
        vendorLoginDoc.RootElement.GetProperty("user").GetProperty("role").GetString().Should().Be("Vendor");
        vendorLoginDoc.RootElement.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);
    }

    private async Task RegisterAndVerifyCustomerAsync(string email, string phone, string password)
    {
        var customerRegister = await _client.PostAsJsonAsync("/api/customers/auth/register", new
        {
            fullName = "Same Person Customer",
            email,
            phone,
            password,
            addressLine = "Customer address"
        });
        var customerRegisterContent = await customerRegister.Content.ReadAsStringAsync();
        customerRegister.StatusCode.Should().Be(HttpStatusCode.OK, customerRegisterContent);
        var customerToken = JsonDocument.Parse(customerRegisterContent).RootElement.GetProperty("registrationToken").GetString();
        var customerOtp = _factory.OtpSink.EmailDispatches.Single(d => d.Recipient == email).OtpCode;
        var customerVerify = await _client.PostAsJsonAsync(
            "/api/customers/auth/verify-otp",
            new { identifier = email, otpCode = customerOtp, registrationToken = customerToken });
        customerVerify.StatusCode.Should().Be(HttpStatusCode.OK, await customerVerify.Content.ReadAsStringAsync());
    }

    private async Task EnsureVendorCsrfAsync()
    {
        var csrf = await _client.GetFromJsonAsync<CsrfTokenResponse>("/api/vendors/auth/csrf");
        _client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        _client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrf!.CsrfToken);
    }

    private static object BuildDriverRegisterBody(string email, string phone, string password)
    {
        var unique = Guid.NewGuid().ToString("N");
        return new
        {
            fullName = "Test Driver",
            email,
            phone,
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

    private static object BuildVendorRegisterBody(string email, string password, string? phone = null)
    {
        var unique = Guid.NewGuid().ToString("N");
        phone ??= "018" + Random.Shared.Next(10000000, 99999999);
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
