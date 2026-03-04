using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zadana.Application.Common.Interfaces;
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
        public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class MockTestOtpService : IOtpService
    {
        public Task SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendOtpEmailAsync(string emailAddress, string otpCode, CancellationToken cancellationToken = default)
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
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        db.Database.EnsureCreated();

        if (!db.Users.Any(u => u.Email == "admin@test.com"))
        {
            db.Users.Add(new Zadana.Domain.Modules.Identity.Entities.User(
                fullName: "Test Admin",
                email: "admin@test.com",
                phone: "01000000001",
                passwordHash: hasher.HashPassword("Admin@123"),
                role: Zadana.Domain.Modules.Identity.Enums.UserRole.SuperAdmin));
            db.SaveChanges();
        }
    }
}
