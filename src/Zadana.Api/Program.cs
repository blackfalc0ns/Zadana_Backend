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
    });
}

builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
builder.Services.AddTransient<IPasswordHasher, Zadana.Infrastructure.Modules.Identity.Services.PasswordHasher>();
builder.Services.Configure<Zadana.Infrastructure.Settings.ImageKitSettings>(
    builder.Configuration.GetSection(Zadana.Infrastructure.Settings.ImageKitSettings.SectionName));

builder.Services.AddTransient<Zadana.Application.Common.Interfaces.IFileStorageService, Zadana.Infrastructure.Services.ImageKitFileStorageService>();

// ───── Security & Auth ─────
builder.Services.AddHttpContextAccessor();
// Add Identity Infrastructure
builder.Services.AddIdentityInfrastructure(builder.Configuration);


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ───── Auto-Migrate & Seed SuperAdmin (skip in Testing environment) ─────
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    // Apply pending migrations automatically
    db.Database.Migrate();

    if (!db.Users.Any(u => u.Role == Zadana.Domain.Modules.Identity.Enums.UserRole.SuperAdmin))
    {
        var admin = new Zadana.Domain.Modules.Identity.Entities.User(
            fullName: "Super Admin",
            email: "admin@system.com",
            phone: "01000000000",
            passwordHash: hasher.HashPassword("Admin@123"),
            role: Zadana.Domain.Modules.Identity.Enums.UserRole.SuperAdmin);

        db.Users.Add(admin);
        db.SaveChanges();
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
