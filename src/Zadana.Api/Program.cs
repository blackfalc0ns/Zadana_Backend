using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Zadana.Api.Configuration;
using Zadana.Api.BackgroundJobs;
using Zadana.Api.Middleware;
using Zadana.Api.Realtime;
using Zadana.Application;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Home.Interfaces;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Infrastructure.Modules.Catalog.Repositories;
using Zadana.Infrastructure.Modules.Catalog.Services;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Infrastructure.Modules.Delivery.Repositories;
using Zadana.Infrastructure.Modules.Home.Services;
using Zadana.Infrastructure.Modules.Identity;
using Zadana.Infrastructure.Modules.Orders.Repositories;
using Zadana.Infrastructure.Modules.Orders.Services;
using Zadana.Infrastructure.Modules.Vendors.Repositories;
using Zadana.Infrastructure.Modules.Vendors.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

var builder = WebApplication.CreateBuilder(args);
var jwtSecret = builder.Configuration.GetRequiredSetting("JwtSettings:Secret");

builder.Services.AddApplication();

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddSingleton<AuditableEntityInterceptor>();
    builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    {
        options.UseSqlServer(
            builder.Configuration.GetRequiredConnectionString("DefaultConnection"),
            sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            });

        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }
    });
    builder.Services.AddScoped<ApplicationDbContextInitialiser>();
}

builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IVendorRepository, VendorRepository>();
builder.Services.AddScoped<IVendorReadService, VendorReadService>();
builder.Services.AddScoped<IVendorReviewAuditService, VendorReviewAuditService>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<IProductRequestRepository, ProductRequestRepository>();
builder.Services.AddScoped<IProductRequestReadService, ProductRequestReadService>();
builder.Services.AddScoped<ICatalogRequestReadService, CatalogRequestReadService>();
builder.Services.AddScoped<IHomeReadService, HomeReadService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderReadService, OrderReadService>();
builder.Services.AddSingleton<CustomerPresenceService>();
builder.Services.AddSingleton<ICustomerPresenceService>(provider => provider.GetRequiredService<CustomerPresenceService>());
builder.Services.AddSingleton<IAdminBrandBulkOperationQueue, AdminBrandBulkOperationQueue>();
builder.Services.AddSingleton<IAdminMasterProductBulkOperationQueue, AdminMasterProductBulkOperationQueue>();
builder.Services.AddSingleton<IVendorProductBulkOperationQueue, VendorProductBulkOperationQueue>();
builder.Services.AddHostedService<CustomerPresenceSweepWorker>();
builder.Services.AddHostedService<AdminBrandBulkOperationWorker>();
builder.Services.AddHostedService<AdminMasterProductBulkOperationWorker>();
builder.Services.AddHostedService<VendorProductBulkOperationWorker>();

builder.Services.AddOptions<Zadana.Infrastructure.Settings.ImageKitSettings>()
    .Bind(builder.Configuration.GetSection(Zadana.Infrastructure.Settings.ImageKitSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddTransient<IFileStorageService, Zadana.Infrastructure.Modules.Files.Services.LocalFileStorageService>();
}
else
{
    builder.Services.AddTransient<IFileStorageService, Zadana.Infrastructure.Services.ImageKitFileStorageService>();
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddIdentityInfrastructure(builder.Configuration);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
            return;
        }

        if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        {
            policy.SetIsOriginAllowed(_ => true)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
            return;
        }

        throw new InvalidOperationException("CORS allowed origins are not configured.");
    });
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments(CustomerPresenceHub.HubRoute))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CustomerOnly", policy => policy.RequireRole("Customer"));
    options.AddPolicy("DriverOnly", policy => policy.RequireRole("Driver"));
    options.AddPolicy("VendorOnly", policy => policy.RequireRole("Vendor", "VendorStaff"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin", "SuperAdmin"));
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });
builder.Services.AddSignalR();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddLocalization();
builder.Services.AddHealthChecks();

var app = builder.Build();
var shouldSeedOnStartup = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Seeding:EnableOnStartup");
var shouldResetOnStartup = app.Configuration.GetValue<bool>("Seeding:ResetOnStartup");
var allowRemoteSeedEndpoints = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Seeding:EnableRemoteEndpoints");
var seedingManagementKey = app.Configuration["Seeding:ManagementKey"];

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseStaticFiles();

var supportedCultures = new[] { "en", "ar" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ar")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<CustomerPresenceHub>(CustomerPresenceHub.HubRoute);

if (shouldSeedOnStartup)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
        await initialiser.InitialiseAsync();

        if (shouldResetOnStartup)
        {
            await initialiser.ResetAndSeedAsync();
        }
        else
        {
            await initialiser.SeedAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database initialization.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapPost("/dev/reset-seed", async (
            ApplicationDbContextInitialiser initialiser,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            await initialiser.InitialiseAsync();
            var summary = await initialiser.ResetAndSeedAsync();

            logger.LogInformation("Development database reset and reseed completed successfully.");

            return Results.Ok(new
            {
                message = "Development database reset and reseed completed successfully.",
                summary
            });
        })
        .WithTags("Development")
        .WithSummary("Reset and reseed the development database")
        .WithDescription("Deletes development data and rebuilds a complete deterministic seed dataset. Available only in Development.");
}

if (allowRemoteSeedEndpoints)
{
    app.MapPost("/ops/seed/run", async (
            HttpContext httpContext,
            ApplicationDbContextInitialiser initialiser,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsAuthorizedSeedRequest(httpContext, seedingManagementKey))
            {
                return Results.Unauthorized();
            }

            await initialiser.InitialiseAsync();
            await initialiser.SeedAsync();

            logger.LogInformation("Seed operation completed successfully via remote management endpoint.");

            return Results.Ok(new
            {
                message = "Seed operation completed successfully."
            });
        })
        .WithTags("Operations")
        .WithSummary("Run seed data on the current environment")
        .WithDescription("Runs the application seed logic on the current environment. Requires X-Seeding-Key.");

    app.MapPost("/ops/seed/reset", async (
            HttpContext httpContext,
            ApplicationDbContextInitialiser initialiser,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsAuthorizedSeedRequest(httpContext, seedingManagementKey))
            {
                return Results.Unauthorized();
            }

            await initialiser.InitialiseAsync();
            var summary = await initialiser.ResetAndSeedAsync();

            logger.LogInformation("Reset and seed operation completed successfully via remote management endpoint.");

            return Results.Ok(new
            {
                message = "Reset and seed operation completed successfully.",
                summary
            });
        })
        .WithTags("Operations")
        .WithSummary("Reset and reseed data on the current environment")
        .WithDescription("Resets and reseeds the database on the current environment. Requires X-Seeding-Key.");
}

app.MapHealthChecks("/health");
app.MapGet("/health/ready", () => Results.Ok(new { status = "Ready", timestamp = DateTime.UtcNow }));

app.Run();

static bool IsAuthorizedSeedRequest(HttpContext httpContext, string? expectedKey)
{
    if (string.IsNullOrWhiteSpace(expectedKey))
    {
        return false;
    }

    if (!httpContext.Request.Headers.TryGetValue("X-Seeding-Key", out var providedKey))
    {
        return false;
    }

    return string.Equals(providedKey.ToString(), expectedKey, StringComparison.Ordinal);
}

public partial class Program { }
