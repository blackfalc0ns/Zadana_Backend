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

        services.AddOptions<WapilotOtpSettings>()
            .Bind(configuration.GetSection(WapilotOtpSettings.SectionName))
            .Validate(settings => !settings.Enabled || !IsPlaceholder(settings.ApiKey),
                "WapilotOtp:ApiKey is required when WapilotOtp:Enabled is true.")
            .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.InstanceId),
                "WapilotOtp:InstanceId is required when WapilotOtp:Enabled is true.")
            .Validate(settings => Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _),
                "WapilotOtp:BaseUrl must be an absolute URL.")
            .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.SendMessagePath),
                "WapilotOtp:SendMessagePath is required when WapilotOtp:Enabled is true.")
            .Validate(settings =>
            {
                try
                {
                    _ = WhatsAppPhoneNumberNormalizer.NormalizeCountryCode(settings.DefaultCountryCode);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }, "WapilotOtp:DefaultCountryCode must be an international dialing code.")
            .ValidateOnStart();

        services.AddOptions<WhatsAppCloudOtpSettings>()
            .Bind(configuration.GetSection(WhatsAppCloudOtpSettings.SectionName))
            .Validate(settings => !settings.Enabled || !IsPlaceholder(settings.AccessToken),
                "WhatsAppCloudOtp:AccessToken is required when WhatsAppCloudOtp:Enabled is true.")
            .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.PhoneNumberId),
                "WhatsAppCloudOtp:PhoneNumberId is required when WhatsAppCloudOtp:Enabled is true.")
            .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.TemplateName),
                "WhatsAppCloudOtp:TemplateName is required when WhatsAppCloudOtp:Enabled is true.")
            .Validate(settings => Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _),
                "WhatsAppCloudOtp:BaseUrl must be an absolute URL.")
            .Validate(settings =>
            {
                try
                {
                    _ = WhatsAppPhoneNumberNormalizer.NormalizeCountryCode(settings.DefaultCountryCode);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }, "WhatsAppCloudOtp:DefaultCountryCode must be an international dialing code.")
            .ValidateOnStart();
        
        services.AddHttpClient<IEmailService, ResendEmailService>();
        services.AddHttpClient<WhatsAppCloudOtpService>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<WhatsAppCloudOtpSettings>>().Value;
            client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(settings.BaseUrl)
                ? "https://graph.facebook.com"
                : settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient<WapilotWhatsAppOtpService>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<WapilotOtpSettings>>().Value;
            client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(settings.BaseUrl)
                ? "https://api.wapilot.net"
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

    private static bool IsPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Trim().StartsWith("__SET_VIA_ENV__", StringComparison.OrdinalIgnoreCase);
}
