using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;
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
        services.AddOptions<ResendEmailSettings>()
            .Bind(configuration.GetSection(ResendEmailSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<TwilioSettings>()
            .Bind(configuration.GetSection(TwilioSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        services.AddHttpClient<IEmailService, ResendEmailService>();

        // Repositories
        services.AddScoped<IIdentityAccountService, IdentityAccountService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenRepository>();

        // Services
        services.AddTransient<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddTransient<IOtpService, ResendOtpService>();
        services.AddTransient<ITemplateService, HtmlTemplateService>();

        return services;
    }
}
