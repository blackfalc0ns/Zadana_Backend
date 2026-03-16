using Microsoft.EntityFrameworkCore;
using Zadana.Application;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Modules.Identity;
using Zadana.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Identity;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);
var jwtSecret = builder.Configuration.GetRequiredSetting("JwtSettings:Secret");

// â”€â”€â”€â”€â”€ Application Layer â”€â”€â”€â”€â”€
builder.Services.AddApplication();

// â”€â”€â”€â”€â”€ Infrastructure: EF Core â”€â”€â”€â”€â”€
// Skip SQL Server registration in Testing environment (WebApplicationFactory provides InMemory instead)
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

// Configure and validate ImageKit settings
builder.Services.Configure<Zadana.Infrastructure.Settings.ImageKitSettings>(
    builder.Configuration.GetSection(Zadana.Infrastructure.Settings.ImageKitSettings.SectionName));

builder.Services.AddTransient<Zadana.Application.Common.Interfaces.IFileStorageService, Zadana.Infrastructure.Services.ImageKitFileStorageService>();

// â”€â”€â”€â”€â”€ Security & Auth â”€â”€â”€â”€â”€
builder.Services.AddHttpContextAccessor();
// Add Identity Infrastructure
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

// Add CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader());
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CustomerOnly", policy =>
        policy.RequireRole("Customer"));

    options.AddPolicy("DriverOnly", policy =>
        policy.RequireRole("Driver"));

    options.AddPolicy("VendorOnly", policy =>
        policy.RequireRole("Vendor", "VendorStaff"));

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin", "SuperAdmin"));
});

// â”€â”€â”€â”€â”€ API â”€â”€â”€â”€â”€
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Suppress default ASP.NET Core validation so FluentValidation can return our localized messages
        options.SuppressModelStateInvalidFilter = true;
    });
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

// â”€â”€â”€â”€â”€ Localization â”€â”€â”€â”€â”€
builder.Services.AddLocalization();

var app = builder.Build();

// â”€â”€â”€â”€â”€ Pipeline â”€â”€â”€â”€â”€
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// Use request localization (ar / en)
var supportedCultures = new[] { "en", "ar" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ar") // Default to Arabic if not specified
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// â”€â”€â”€â”€â”€ Auto-Migrate & Seed (skip in Testing environment) â”€â”€â”€â”€â”€
if (!app.Environment.IsEnvironment("Testing"))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
        await initialiser.InitialiseAsync();
        await initialiser.SeedAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database initialization.");
        // We don't rethrow here to allow the app to start even if DB is problematic, 
        // enabling health checks and Swagger to be accessible for debugging.
    }
}

// Health check endpoint
app.MapGet("/health", (ApplicationDbContext db) =>
{
    var canConnect = db.Database.CanConnect();
    return Results.Ok(new
    {
        status = canConnect ? "Healthy" : "Unhealthy",
        database = canConnect ? "Connected" : "Disconnected",
        timestamp = DateTime.UtcNow
    });
});

app.Run();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }

