using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Application.Tests.Helpers;

/// <summary>
/// Custom WebApplicationFactory that:
/// - Uses Environment = "Testing" → Program.cs skips SqlServer and db.Database.Migrate()
/// - Registers an isolated InMemory database per test class instance
/// - Injects test JWT configuration so token generation works
/// </summary>
public class ZadanaWebFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Inject test-only JWT config
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "TestSecretKey_For_Integration_Tests_Only_32chars!",
                ["JwtSettings:Issuer"] = "ZadanaTest",
                ["JwtSettings:Audience"] = "ZadanaTestClient",
                ["JwtSettings:ExpiryMinutes"] = "60",
                // Dummy Twilio settings for testing (won't be used)
                ["TwilioSettings:AccountSid"] = "ACtest",
                ["TwilioSettings:AuthToken"] = "test_token",
                ["TwilioSettings:FromNumber"] = "+10000000000"
            });
        });

        builder.ConfigureServices(services =>
        {
            // AuditableEntityInterceptor is required by ApplicationDbContext constructor
            services.AddSingleton<Zadana.Infrastructure.Persistence.Interceptors.AuditableEntityInterceptor>();

            // Register the InMemory database (Program.cs skips SqlServer in Testing env)
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Replace real services with mocks for testing
            services.AddScoped<IEmailService, MockEmailService>();
            services.RemoveAll<IOtpService>();
            services.AddTransient<IOtpService, MockTestOtpService>();
        });
    }

    private class MockEmailService : IEmailService
    {
        public Task<EmailSendResult> SendEmailAsync(SendEmailRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EmailSendResult("mock", true, Guid.NewGuid().ToString("N"), null));
        }
    }

    private class MockTestOtpService : IOtpService
    {
        public Task SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendOtpEmailAsync(string emailAddress, string otpCode, CancellationToken cancellationToken = default, int validityMinutes = 5)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Seeds test data after the host is fully built.
    /// </summary>
    public void SeedTestData()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        db.Database.EnsureCreated();

        if (!db.Users.Any(u => u.Email == "admin@test.com"))
        {
            var admin = new User(
                fullName: "Test Admin",
                email: "admin@test.com",
                phone: "01000000001",
                role: UserRole.SuperAdmin);

            var result = userManager.CreateAsync(admin, "Admin@123").GetAwaiter().GetResult();
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create seeded admin user: {string.Join(", ", result.Errors.Select(error => error.Description))}");
            }
        }
    }
}
