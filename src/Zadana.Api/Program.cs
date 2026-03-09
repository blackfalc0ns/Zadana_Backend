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

var builder = WebApplication.CreateBuilder(args);

// ───── Application Layer ─────
builder.Services.AddApplication();

// ───── Infrastructure: EF Core ─────
// Skip SQL Server registration in Testing environment (WebApplicationFactory provides InMemory instead)
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddSingleton<AuditableEntityInterceptor>();
    builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    {
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
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
}

builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
builder.Services.Configure<Zadana.Infrastructure.Settings.ImageKitSettings>(
    builder.Configuration.GetSection(Zadana.Infrastructure.Settings.ImageKitSettings.SectionName));

builder.Services.AddTransient<Zadana.Application.Common.Interfaces.IFileStorageService, Zadana.Infrastructure.Services.ImageKitFileStorageService>();

// ───── Security & Auth ─────
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
                Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]!))
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

// ───── API ─────
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

// ───── Localization ─────
builder.Services.AddLocalization();

var app = builder.Build();

// ───── Pipeline ─────
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

// ───── Auto-Migrate & Seed SuperAdmin (skip in Testing environment) ─────
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    // Apply pending migrations automatically
    db.Database.Migrate();

    var superAdminRoleExists = roleManager.RoleExistsAsync(Zadana.Domain.Modules.Identity.Enums.UserRole.SuperAdmin.ToString()).GetAwaiter().GetResult();
    if (!superAdminRoleExists)
    {
        roleManager.CreateAsync(new IdentityRole<Guid>(Zadana.Domain.Modules.Identity.Enums.UserRole.SuperAdmin.ToString())).GetAwaiter().GetResult();
        roleManager.CreateAsync(new IdentityRole<Guid>(Zadana.Domain.Modules.Identity.Enums.UserRole.Admin.ToString())).GetAwaiter().GetResult();
        roleManager.CreateAsync(new IdentityRole<Guid>(Zadana.Domain.Modules.Identity.Enums.UserRole.Vendor.ToString())).GetAwaiter().GetResult();
        roleManager.CreateAsync(new IdentityRole<Guid>(Zadana.Domain.Modules.Identity.Enums.UserRole.VendorStaff.ToString())).GetAwaiter().GetResult();
        roleManager.CreateAsync(new IdentityRole<Guid>(Zadana.Domain.Modules.Identity.Enums.UserRole.Customer.ToString())).GetAwaiter().GetResult();
        roleManager.CreateAsync(new IdentityRole<Guid>(Zadana.Domain.Modules.Identity.Enums.UserRole.Driver.ToString())).GetAwaiter().GetResult();
    }

    if (userManager.FindByEmailAsync("admin@system.com").GetAwaiter().GetResult() == null)
    {
        var admin = new User(
            "Super Admin",
            "admin@system.com",
            "01000000000",
            Zadana.Domain.Modules.Identity.Enums.UserRole.SuperAdmin);

        var result = userManager.CreateAsync(admin, "Admin@123").GetAwaiter().GetResult();
        if (result.Succeeded)
        {
            userManager.AddToRoleAsync(admin, Zadana.Domain.Modules.Identity.Enums.UserRole.SuperAdmin.ToString()).GetAwaiter().GetResult();
        }
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
