using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Zadana.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.Infrastructure.Modules.Identity.Repositories;
using Zadana.Infrastructure.Modules.Identity.Services;

namespace Zadana.Infrastructure.Modules.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Services
        services.AddTransient<IJwtTokenService, JwtTokenService>();
        services.AddTransient<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}
