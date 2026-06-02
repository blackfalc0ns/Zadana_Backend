using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Infrastructure.Modules.Identity.Repositories;
using Zadana.Infrastructure.Modules.Identity.Services;
using Zadana.Infrastructure.Services;
using Zadana.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Zadana.Infrastructure.Settings;

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

        services.AddOptions<NabdaOtpSettings>()
            .Bind(configuration.GetSection(NabdaOtpSettings.SectionName))
            .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.ApiKey),
                "NabdaOtp:ApiKey is required when NabdaOtp:Enabled is true.")
            .Validate(settings => Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _),
                "NabdaOtp:BaseUrl must be an absolute URL.")
            .Validate(settings =>
            {
                try
                {
                    _ = NabdaPhoneNumberNormalizer.NormalizeCountryCode(settings.DefaultCountryCode);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }, "NabdaOtp:DefaultCountryCode must be an international dialing code.")
            .ValidateOnStart();
        
        services.AddHttpClient<IEmailService, ResendEmailService>();
        services.AddHttpClient<NabdaWhatsAppOtpService>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<NabdaOtpSettings>>().Value;
            client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(settings.BaseUrl)
                ? "https://api.nabdaotp.com"
                : settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // Repositories
        services.AddScoped<IIdentityAccountService, IdentityAccountService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenRepository>();
        services.AddScoped<IAccessControlService, AccessControlService>();

        // Services
        services.AddTransient<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddTransient<ResendOtpService>();
        services.AddTransient<IOtpService, CompositeOtpService>();
        services.AddTransient<ITemplateService, HtmlTemplateService>();

        return services;
    }
}
