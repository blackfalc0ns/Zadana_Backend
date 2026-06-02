using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Geography.Entities;
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
    private readonly object _seedSync = new();
    private bool _seeded;
    public TestOtpSink OtpSink { get; } = new();

    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();
        EnsureSeeded();
        return client;
    }

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
            services.AddSingleton(OtpSink);
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

    public sealed class TestOtpSink
    {
        private readonly List<TestOtpDispatch> _smsDispatches = [];
        private readonly List<TestOtpDispatch> _emailDispatches = [];
        private readonly object _sync = new();

        public IReadOnlyList<TestOtpDispatch> SmsDispatches
        {
            get
            {
                lock (_sync)
                {
                    return _smsDispatches.ToArray();
                }
            }
        }

        public IReadOnlyList<TestOtpDispatch> EmailDispatches
        {
            get
            {
                lock (_sync)
                {
                    return _emailDispatches.ToArray();
                }
            }
        }

        public void RecordSms(string recipient, string otpCode)
        {
            lock (_sync)
            {
                _smsDispatches.Add(new TestOtpDispatch(recipient, otpCode));
            }
        }

        public void RecordEmail(string recipient, string otpCode)
        {
            lock (_sync)
            {
                _emailDispatches.Add(new TestOtpDispatch(recipient, otpCode));
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _smsDispatches.Clear();
                _emailDispatches.Clear();
            }
        }
    }

    public sealed record TestOtpDispatch(string Recipient, string OtpCode);

    private class MockTestOtpService : IOtpService
    {
        private readonly TestOtpSink _sink;

        public MockTestOtpService(TestOtpSink sink)
        {
            _sink = sink;
        }

        public Task SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
        {
            _sink.RecordSms(phoneNumber, otpCode);
            return Task.CompletedTask;
        }

        public Task SendOtpEmailAsync(string emailAddress, string otpCode, CancellationToken cancellationToken = default, int validityMinutes = 5)
        {
            _sink.RecordEmail(emailAddress, otpCode);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Seeds test data after the host is fully built.
    /// </summary>
    public void SeedTestData()
    {
        EnsureSeeded();
    }

    private void EnsureSeeded()
    {
        lock (_seedSync)
        {
            if (_seeded)
            {
                return;
            }

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            db.Database.EnsureCreated();

            foreach (var roleName in Enum.GetNames<UserRole>())
            {
                if (!roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult())
                {
                    var roleResult = roleManager.CreateAsync(new IdentityRole<Guid>(roleName)).GetAwaiter().GetResult();
                    if (!roleResult.Succeeded)
                    {
                        throw new InvalidOperationException($"Failed to create seeded role {roleName}: {string.Join(", ", roleResult.Errors.Select(error => error.Description))}");
                    }
                }
            }

            if (!db.SaudiRegions.Any(r => r.Code == "RIYADH"))
            {
                var regionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
                db.SaudiRegions.Add(new SaudiRegion(
                    regionId,
                    "RIYADH",
                    "الرياض",
                    "Riyadh",
                    24.7136,
                    46.6753,
                    9,
                    1));
                db.SaudiCities.Add(new SaudiCity(
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    regionId,
                    "RIYADH_CITY",
                    "الرياض",
                    "Riyadh",
                    24.7136,
                    46.6753,
                    12,
                    1));
                db.SaveChanges();
            }

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

            _seeded = true;
        }
    }
}
