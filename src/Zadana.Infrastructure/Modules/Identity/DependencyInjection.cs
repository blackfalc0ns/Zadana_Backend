using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.Infrastructure.Modules.Identity.Repositories;
using Zadana.Infrastructure.Modules.Identity.Services;
using Zadana.Infrastructure.Services;
using Zadana.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Resend;

namespace Zadana.Infrastructure.Modules.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Resend Email Service
        services.Configure<ResendEmailSettings>(configuration.GetSection(ResendEmailSettings.SectionName));
        
        // Manual registration of Resend client
        services.AddHttpClient<IResend, ResendClient>(client =>
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", configuration[$"{ResendEmailSettings.SectionName}:ApiKey"]);
        });

        services.AddScoped<IEmailService, ResendEmailService>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Services
        services.AddTransient<IJwtTokenService, JwtTokenService>();
        services.AddTransient<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddTransient<IOtpService, MockOtpService>();

        return services;
    }
}
