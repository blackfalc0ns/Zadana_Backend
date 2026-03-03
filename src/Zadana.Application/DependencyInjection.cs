using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Zadana.Application.Common.Behaviors;
using Zadana.Application.Common.Localization;

namespace Zadana.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Configure FluentValidation to use our custom Arabic/English resources
        ValidatorOptions.Global.LanguageManager.Enabled = false; // Disable default internal translations

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddScoped<Zadana.Application.Modules.Identity.Interfaces.IIdentityService, Zadana.Application.Modules.Identity.Services.IdentityService>();

        return services;
    }
}
