using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zadana.SharedKernel.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Zadana.Api.Authorization;
using Zadana.Api.Configuration;
using Zadana.Api.BackgroundJobs;
using Zadana.Api.Middleware;
using Zadana.Api.Realtime;
using Zadana.Api.Security;
using Zadana.Application;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Application.Modules.Catalog.Services;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Home.Interfaces;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Wallets.Interfaces;
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
using Zadana.Infrastructure.Caching;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.Infrastructure.Services;
using Zadana.Infrastructure.Settings;

var builder = WebApplication.CreateBuilder(args);

// Remove EventLog provider on Windows — it gets disposed before BackgroundServices
// finish during graceful shutdown, causing ObjectDisposedException crashes.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// Prevent background service exceptions from crashing the host
builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

// In local development, re-apply user secrets after the default providers so
// stale environment variables do not override freshly rotated local secrets.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
}

// Allow developers to keep secrets out of source control by overriding any
// setting via appsettings.Local.json (gitignored) or environment variables.
// Order: appsettings.json -> appsettings.{env}.json -> appsettings.Local.json
//        -> user-secrets (Development) -> environment variables.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables(prefix: "ZADANA_");
builder.Configuration.AddEnvironmentVariables();

var jwtSecret = builder.Configuration.GetRequiredSetting("JwtSettings:Secret");
var realtimeWebSocketsEnabled = builder.Configuration.GetValue("Realtime:WebSocketsEnabled", true);
var realtimeServerSentEventsEnabled = builder.Configuration.GetValue("Realtime:ServerSentEventsEnabled", true);
var realtimeAllowQueryStringAccessTokens = builder.Configuration.GetValue("Realtime:AllowQueryStringAccessTokens", false);
var fileStorageProvider = builder.Configuration[$"{FileStorageSettings.SectionName}:Provider"]?.Trim();
fileStorageProvider = string.IsNullOrWhiteSpace(fileStorageProvider) ? "ImageKit" : fileStorageProvider;
var useLocalFileStorage = fileStorageProvider.Equals("Local", StringComparison.OrdinalIgnoreCase);

// Production hardening: refuse to start if critical settings are still
// placeholders or default values that have ever been committed.
if (builder.Environment.IsProduction())
{
    var requiredProductionSettings = new List<string>
    {
        "JwtSettings:Secret",
        "Moyasar:SecretKey",
        "Moyasar:WebhookSecret",
        "Email:Smtp:Host",
        "Email:Smtp:Username",
        "Email:Smtp:Password",
        "BankTransfer:WebhookSecret",
        "Security:SearchableHashKey"
    };

    if (useLocalFileStorage)
    {
        requiredProductionSettings.Add("FileStorage:Local:RootPath");
        requiredProductionSettings.Add("FileStorage:Local:PublicBaseUrl");
    }
    else
    {
        requiredProductionSettings.Add("ImageKit:PrivateKey");
    }

    if (builder.Configuration.GetValue<bool>("WapilotOtp:Enabled"))
    {
        requiredProductionSettings.Add("WapilotOtp:ApiKey");
        requiredProductionSettings.Add("WapilotOtp:InstanceId");
    }

    if (builder.Configuration.GetValue<bool>("WhatsAppCloudOtp:Enabled"))
    {
        requiredProductionSettings.Add("WhatsAppCloudOtp:AccessToken");
        requiredProductionSettings.Add("WhatsAppCloudOtp:PhoneNumberId");
        requiredProductionSettings.Add("WhatsAppCloudOtp:TemplateName");
    }

    var missing = requiredProductionSettings
        .Where(key => Zadana.Api.Configuration.ConfigurationGuardExtensions.IsPlaceholder(builder.Configuration[key]))
        .ToArray();

    if (missing.Length > 0)
    {
        throw new InvalidOperationException(
            "Production startup blocked: the following required secrets are not configured (set them via environment variables): " +
            string.Join(", ", missing));
    }

    // JWT Secret length: HS256 needs at least 32 bytes of entropy.
    if (System.Text.Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    {
        throw new InvalidOperationException(
            "Production startup blocked: JwtSettings:Secret must be at least 32 bytes (use a 64-byte random value).");
    }

    // Connection string must enforce TLS to the database. Some shared
    // hosting providers (databaseasp.net, runasp.net) ship with self-signed
    // certificates, so TrustServerCertificate=True is allowed; what we
    // strictly forbid is unencrypted SQL traffic over the wire.
    var prodConn = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    if (prodConn.Contains("Encrypt=False", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Production startup blocked: ConnectionStrings:DefaultConnection must use Encrypt=True. " +
            "If your DB provider uses a self-signed certificate, also set TrustServerCertificate=True.");
    }
}
var cachingSettingsSection = builder.Configuration.GetSection(CachingSettings.SectionName);
var cachingSettings = cachingSettingsSection.Get<CachingSettings>() ?? new CachingSettings();
var redisConnectionString = cachingSettings.Redis.ConnectionString;
var useRedisCaching = !string.IsNullOrWhiteSpace(redisConnectionString);
var databasePerformanceSection = builder.Configuration.GetSection(DatabasePerformanceSettings.SectionName);

builder.Services.AddApplication();
builder.Services.AddOptions<CachingSettings>()
    .Bind(cachingSettingsSection)
    .Validate(settings => settings.MaximumPayloadBytes > 0, "Caching maximum payload bytes must be greater than zero.")
    .Validate(settings => settings.MaximumKeyLength > 0, "Caching maximum key length must be greater than zero.")
    .Validate(
        settings => !builder.Environment.IsProduction() || !settings.Redis.RequireInProduction || !string.IsNullOrWhiteSpace(settings.Redis.ConnectionString),
        "Caching:Redis:ConnectionString is required when Redis is enforced in production.")
    .ValidateOnStart();
builder.Services.AddOptions<DatabasePerformanceSettings>()
    .Bind(databasePerformanceSection)
    .Validate(
        settings => settings.SlowQueryThresholdMilliseconds >= 100,
        "DatabasePerformance:SlowQueryThresholdMilliseconds must be at least 100.")
    .Validate(
        settings => settings.MaxLoggedCommandTextLength is >= 100 and <= 4000,
        "DatabasePerformance:MaxLoggedCommandTextLength must be between 100 and 4000.")
    .ValidateOnStart();

if (!builder.Environment.IsEnvironment("Testing"))
{
    // Register the interceptor as a singleton — it doesn't hold per-request
    // state and resolves the current user via a child scope when needed,
    // which makes it safe to reuse across pooled DbContext instances.
    builder.Services.AddSingleton<AuditableEntityInterceptor>(sp =>
        new AuditableEntityInterceptor(sp));
    builder.Services.AddSingleton<SlowQueryLoggingInterceptor>();

    // DbContext pooling reuses change-tracker / model-cache state across
    // requests, eliminating per-request allocations and dramatically lowering
    // CPU under load. poolSize=256 prevents pool exhaustion under spikes.
    builder.Services.AddDbContextPool<ApplicationDbContext>((sp, options) =>
    {
        var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
        var slowQueryInterceptor = sp.GetRequiredService<SlowQueryLoggingInterceptor>();
        options.UseSqlServer(
            builder.Configuration.GetRequiredConnectionString("DefaultConnection"),
            sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
                sqlOptions.CommandTimeout(60);
            });

        options.AddInterceptors(interceptor, slowQueryInterceptor);

        // PII value converters are bound to the runtime IDataProtector and
        // therefore cannot be represented faithfully in an EF migration
        // snapshot. EF Core 9 can consequently report pending model changes
        // at runtime even when the schema model and committed snapshot are in
        // sync. Keep the check strict outside Production, but do not let this
        // false positive prevent committed migrations from running.
        if (builder.Environment.IsProduction())
        {
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        }

        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }
    }, poolSize: 256);

    builder.Services.AddScoped<ApplicationDbContextInitialiser>();
}

builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IApplicationTransaction, ApplicationTransaction>();
builder.Services.AddScoped<ICatalogReadCacheService, CatalogReadCacheService>();
builder.Services.AddScoped<IVendorRepository, VendorRepository>();
builder.Services.AddScoped<IVendorReadService, VendorReadService>();
builder.Services.AddScoped<IVendorReviewAuditService, VendorReviewAuditService>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddSingleton<Zadana.Application.Common.Interfaces.IGeographyCityResolver, Zadana.Infrastructure.Modules.Geography.Services.GeographyCityResolver>();
builder.Services.AddScoped<IDriverReadService, Zadana.Infrastructure.Modules.Delivery.Services.DriverReadService>();
builder.Services.AddScoped<IDriverHomeReadService, Zadana.Infrastructure.Modules.Delivery.Services.DriverHomeReadService>();
builder.Services.AddScoped<IDriverWalletReadService, Zadana.Infrastructure.Modules.Delivery.Services.DriverWalletReadService>();
builder.Services.AddScoped<IDriverCommitmentPolicyService, Zadana.Infrastructure.Modules.Delivery.Services.DriverCommitmentPolicyService>();
builder.Services.AddScoped<IDeliveryDispatchService, Zadana.Infrastructure.Modules.Delivery.Services.DeliveryDispatchService>();
builder.Services.AddScoped<Zadana.Application.Modules.Delivery.Support.DeliveryAssignmentOrderCancellationService>();
builder.Services.AddSingleton<Zadana.Infrastructure.Modules.Delivery.Services.DeliveryPricingCacheService>();
builder.Services.AddScoped<IDeliveryPricingService, Zadana.Infrastructure.Modules.Delivery.Services.DeliveryPricingService>();
builder.Services.AddScoped<IProductRequestRepository, ProductRequestRepository>();
builder.Services.AddScoped<IProductRequestReadService, ProductRequestReadService>();
builder.Services.AddScoped<ICatalogRequestReadService, CatalogRequestReadService>();
builder.Services.AddScoped<IAdminBrandBulkOperationProcessor, AdminBrandBulkOperationProcessor>();
builder.Services.AddScoped<IAdminMasterProductBulkOperationProcessor, AdminMasterProductBulkOperationProcessor>();
builder.Services.AddScoped<IVendorProductBulkOperationProcessor, VendorProductBulkOperationProcessor>();
builder.Services.AddScoped<IHomeReadService, HomeReadService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderReadService, OrderReadService>();
builder.Services.AddScoped<OrderRevenueDistributionService>();
builder.Services.AddScoped<VendorPayoutWalletService>();
builder.Services.AddScoped<VendorRecoveryService>();
builder.Services.AddSingleton<CustomerPresenceService>();
builder.Services.AddSingleton<ICustomerPresenceService>(provider => provider.GetRequiredService<CustomerPresenceService>());
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddSingleton<Zadana.Application.Common.Interfaces.INotificationService>(provider => provider.GetRequiredService<NotificationService>());
builder.Services.AddSingleton<Zadana.Application.Common.Interfaces.IOrderTrackingRealtimeNotifier, OrderTrackingRealtimeNotifier>();
builder.Services.AddScoped<IAdminAlertService, AdminAlertService>();
builder.Services.AddSingleton<IAdminBrandBulkOperationQueue, AdminBrandBulkOperationQueue>();
builder.Services.AddSingleton<IAdminMasterProductBulkOperationQueue, AdminMasterProductBulkOperationQueue>();
builder.Services.AddSingleton<IVendorProductBulkOperationQueue, VendorProductBulkOperationQueue>();
builder.Services.AddHostedService<CustomerPresenceSweepWorker>();
builder.Services.AddHostedService<PendingPaymentExpirationWorker>();
builder.Services.AddHostedService<PaymentProviderEventInboxWorker>();
builder.Services.AddHostedService<PayoutStatusSyncWorker>();
builder.Services.AddHostedService<DeliveryDispatchWorker>();
builder.Services.AddHostedService<AdminBrandBulkOperationWorker>();
builder.Services.AddHostedService<AdminMasterProductBulkOperationWorker>();
builder.Services.AddHostedService<VendorProductBulkOperationWorker>();
builder.Services.AddHostedService<AdminAlertOutboxWorker>();
builder.Services.AddHostedService<NotificationCleanupWorker>();
builder.Services.AddHostedService<VendorSettlementCycleWorker>();
builder.Services.AddHostedService<VendorWeeklySummaryEmailWorker>();
builder.Services.AddHostedService<SupportCaseSlaWorker>();

// SystemLog pipeline: middleware enqueues into a bounded channel, the
// worker drains and writes batches to SQL. This removes the per-request DB
// INSERT from the hot path under load.
builder.Services.AddSingleton<ISystemLogQueue, SystemLogQueue>();
builder.Services.AddHostedService<SystemLogPersistenceWorker>();

// One-shot startup backfill that hashes any existing Driver.NationalId
// records into the new NationalIdHash column. Idempotent and gated by
// Security:RunNationalIdHashBackfill so it can be disabled after first run.
builder.Services.AddHostedService<DriverNationalIdHashBackfillTask>();
builder.Services.AddHostedService<VendorPiiEncryptionBackfillTask>();

builder.Services.AddOptions<FinancialSettingsOptions>()
    .Bind(builder.Configuration.GetSection(FinancialSettingsOptions.SectionName));

builder.Services.AddOptions<BankTransferSettingsOptions>()
    .Bind(builder.Configuration.GetSection(BankTransferSettingsOptions.SectionName));

builder.Services.AddOptions<Zadana.Infrastructure.Settings.ImageKitSettings>()
    .Bind(builder.Configuration.GetSection(Zadana.Infrastructure.Settings.ImageKitSettings.SectionName))
    .ValidateDataAnnotations();

// Payout evidence is stored as protected database attachments rather than a
// generic/public media URL. Keep this scoped with the request DbContext.
builder.Services.AddScoped<Zadana.Api.Modules.Finances.Services.PayoutProofAttachmentService>();

var fileStorageOptionsBuilder = builder.Services.AddOptions<FileStorageSettings>()
    .Bind(builder.Configuration.GetSection(FileStorageSettings.SectionName))
    .Validate(
        settings => settings.Provider.Equals("ImageKit", StringComparison.OrdinalIgnoreCase) ||
                    settings.Provider.Equals("Local", StringComparison.OrdinalIgnoreCase),
        "FileStorage:Provider must be ImageKit or Local.");

if (useLocalFileStorage && !builder.Environment.IsEnvironment("Testing"))
{
    fileStorageOptionsBuilder
        .Validate(
            settings => !string.IsNullOrWhiteSpace(settings.Local.RootPath),
            "FileStorage:Local:RootPath is required for local media storage.")
        .Validate(
            settings => Uri.TryCreate(settings.Local.PublicBaseUrl, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp),
            "FileStorage:Local:PublicBaseUrl must be an absolute HTTP(S) URL.")
        .ValidateOnStart();
}
else if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.PostConfigure<FileStorageSettings>(settings =>
    {
        settings.Provider = "Local";
        settings.Local.RootPath = string.IsNullOrWhiteSpace(settings.Local.RootPath)
            ? Path.Combine(Path.GetTempPath(), "zadana-tests-media")
            : settings.Local.RootPath;
        settings.Local.PublicBaseUrl = string.IsNullOrWhiteSpace(settings.Local.PublicBaseUrl)
            ? "http://localhost/media"
            : settings.Local.PublicBaseUrl;
    });
}

builder.Services.AddOptions<Zadana.Infrastructure.Settings.MoyasarSettings>()
    .Bind(builder.Configuration.GetSection(Zadana.Infrastructure.Settings.MoyasarSettings.SectionName));

builder.Services.AddOptions<OneSignalSettings>()
    .Bind(builder.Configuration.GetSection(OneSignalSettings.SectionName));

// Provider-agnostic payment gateway abstraction (revised SAR-only workflow).
// Moyasar is the sole online card gateway. The resolver hands callers the
// right implementation by name and filters disabled gateways.
builder.Services.AddHttpClient<Zadana.Infrastructure.Services.Payments.MoyasarPaymentGateway>((serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Zadana.Infrastructure.Settings.MoyasarSettings>>().Value;
    client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://api.moyasar.com/v1/" : settings.BaseUrl);
})
.AddStandardResilienceHandler(o =>
{
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(25);
    o.Retry.MaxRetryAttempts = 2;
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    o.CircuitBreaker.FailureRatio = 0.5;
    o.CircuitBreaker.MinimumThroughput = 10;
});
builder.Services.AddTransient<Zadana.Application.Modules.Payments.Interfaces.IPaymentGateway>(sp =>
    sp.GetRequiredService<Zadana.Infrastructure.Services.Payments.MoyasarPaymentGateway>());
builder.Services.AddSingleton<Zadana.Application.Modules.Payments.Interfaces.IPaymentGatewayResolver, Zadana.Infrastructure.Services.Payments.PaymentGatewayResolver>();
builder.Services.AddHttpClient<Zadana.Infrastructure.Services.Payments.MoyasarPayoutGateway>((serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Zadana.Infrastructure.Settings.MoyasarSettings>>().Value;
    client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://api.moyasar.com/v1/" : settings.BaseUrl);
})
.AddStandardResilienceHandler(o =>
{
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(25);
    o.Retry.MaxRetryAttempts = 2;
});
builder.Services.AddTransient<Zadana.Application.Modules.Payments.Interfaces.IPayoutGateway>(sp =>
    sp.GetRequiredService<Zadana.Infrastructure.Services.Payments.MoyasarPayoutGateway>());

builder.Services.AddHttpClient<IOneSignalPushService, OneSignalPushService>((serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OneSignalSettings>>().Value;
    client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://api.onesignal.com" : settings.BaseUrl);
})
.AddStandardResilienceHandler(o =>
{
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
    o.Retry.MaxRetryAttempts = 2;
});

if (builder.Environment.IsEnvironment("Testing") || useLocalFileStorage)
{
    builder.Services.AddSingleton<IFileStorageService, Zadana.Infrastructure.Modules.Files.Services.LocalFileStorageService>();
}
else
{
    builder.Services.AddTransient<IFileStorageService, Zadana.Infrastructure.Services.ImageKitFileStorageService>();
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<RegistrationUploadTokenService>();
builder.Services.AddSingleton<Zadana.Api.Security.GuestCartSigner>();

// Bot challenge (CAPTCHA) — Cloudflare Turnstile by default. Disabled when
// BotChallenge:SecretKey is not configured so dev/local flows still work.
builder.Services.AddHttpClient<Zadana.Application.Common.Interfaces.IBotChallengeService, Zadana.Infrastructure.Services.TurnstileBotChallengeService>();

// JWT revocation list (used by JwtRevocationMiddleware + logout / admin ban).
builder.Services.AddScoped<Zadana.Application.Common.Interfaces.IJwtRevocationStore,
    Zadana.Infrastructure.Modules.Identity.Services.JwtRevocationStore>();

// PII access audit (every read/write of NationalId, IBAN, etc. is logged).
builder.Services.AddScoped<Zadana.Application.Common.Interfaces.IPiiAccessAuditService,
    Zadana.Infrastructure.Modules.Identity.Services.PiiAccessAuditService>();

builder.Services.AddMemoryCache();
SharedRedisConnection? sharedRedisConnection = null;
if (useRedisCaching)
{
    sharedRedisConnection = new SharedRedisConnection(
        redisConnectionString!,
        $"{cachingSettings.Redis.InstanceName}-{builder.Environment.EnvironmentName}");
    var redisConnectionOwnedByContainer = sharedRedisConnection;
    builder.Services.AddSingleton<SharedRedisConnection>(_ => redisConnectionOwnedByContainer);

    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.ConnectionMultiplexerFactory = sharedRedisConnection.GetConnectionAsync;
        options.InstanceName = $"{cachingSettings.Redis.InstanceName}:data:";
    });

    builder.Services.AddStackExchangeRedisOutputCache(options =>
    {
        options.ConnectionMultiplexerFactory = sharedRedisConnection.GetConnectionAsync;
        options.InstanceName = $"{cachingSettings.Redis.InstanceName}:output:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = cachingSettings.MaximumPayloadBytes;
    options.MaximumKeyLength = cachingSettings.MaximumKeyLength;
});
builder.Services.AddSingleton<HybridAppCache>();
builder.Services.AddSingleton<IAppCache>(provider => provider.GetRequiredService<HybridAppCache>());
builder.Services.AddSingleton<ICacheInvalidator>(provider => provider.GetRequiredService<HybridAppCache>());
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy(OutputCachePolicyNames.Geography, policy =>
        policy.Expire(cachingSettings.Durations.Geography)
            .SetVaryByHeader("Accept-Language"));

    options.AddPolicy(OutputCachePolicyNames.CatalogMetadata, policy =>
        policy.Expire(cachingSettings.Durations.PublicCatalogMetadata)
            .SetVaryByHeader("Accept-Language"));

    // Public catalog browse: anonymous & authenticated requests for the same
    // filters return identical data, so we cache aggressively for 60s.
    // Authenticated requests still flow through because OutputCache only
    // caches anonymous traffic by default. NoCache() is overridden via
    // SetVaryByQuery so personalized fields are not leaked.
    options.AddPolicy(OutputCachePolicyNames.PublicCatalogBrowse, policy =>
        policy
            .Expire(TimeSpan.FromSeconds(60))
            .SetVaryByQuery(
                "categoryId", "subcategoryId", "brandId", "productTypeId",
                "partId", "quantityId", "packageTypeId", "minPrice", "maxPrice",
                "category_id", "subcategory_id", "brand_id", "product_type_id",
                "part_id", "quantity_id", "package_type_id", "min_price", "max_price",
                "address_id", "city",
                "sort", "page", "perPage", "per_page", "search", "query")
            .SetVaryByHeader("Accept-Language")
            .Tag("catalog-browse"));

    // Home feed for anonymous customers — heavy aggregate query, but the
    // payload is identical for every guest so a short shared cache wins.
    options.AddPolicy(OutputCachePolicyNames.HomePublic, policy =>
        policy
            .Expire(TimeSpan.FromSeconds(cachingSettings.Durations.HomePublic.TotalSeconds > 0
                ? cachingSettings.Durations.HomePublic.TotalSeconds
                : 120))
            .SetVaryByHeader("Accept-Language")
            .Tag("home-public"));
});
builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
{
    // Password policy: keep backward-compatible (existing users with 8 chars
    // continue to work), but enforce slightly stronger rules for new ones.
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    // Lockout: unchanged behavior, keep existing thresholds.
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

    // User policy
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// DataProtection: persist keys to disk so cookies, anti-forgery tokens, and
// any encrypted state survive process restarts and multi-instance deploys.
// In Production we expect a writable persistent volume mounted at the path
// "DataProtection:KeysPath" (overridable via env var). Falls back to the
// content root in Development.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
}
else if (!Path.IsPathRooted(dataProtectionKeysPath))
{
    dataProtectionKeysPath = Path.GetFullPath(dataProtectionKeysPath, builder.Environment.ContentRootPath);
}

try
{
    Directory.CreateDirectory(dataProtectionKeysPath);

    builder.Services
        .AddDataProtection()
        .SetApplicationName("Zadana.Api")
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}
catch (Exception ex)
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            $"Production startup blocked: DataProtection keys cannot be persisted to '{dataProtectionKeysPath}'. " +
            "Starting with ephemeral keys would make encrypted PII unreadable after a restart.",
            ex);
    }

    Console.Error.WriteLine(
        $"[DataProtection] Failed to persist keys to '{dataProtectionKeysPath}'. " +
        $"Development will use ephemeral keys: {ex.Message}");
    builder.Services
        .AddDataProtection()
        .SetApplicationName("Zadana.Api");
}

// Forwarded headers: when running behind a reverse proxy / IIS / Azure App Service,
// trust X-Forwarded-For/Proto so RemoteIp and IsHttps reflect the real client.
// We clear KnownNetworks/KnownProxies lists in non-Development to allow the
// hosting platform's loopback proxy to forward; restrict further if needed.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.ForwardLimit = 2;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            code = "RATE_LIMIT_EXCEEDED",
            message = Zadana.Api.Localization.ApiLocalizedMessages.Resolve(context.HttpContext, "RATE_LIMIT_EXCEEDED")
        }, cancellationToken);
    };

    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
            RateLimitPartition.GetNoLimiter("testing-global"));
        options.AddPolicy(RateLimitPolicyNames.Auth, _ =>
            RateLimitPartition.GetNoLimiter("testing-auth"));
        options.AddPolicy(RateLimitPolicyNames.FileUploads, _ =>
            RateLimitPartition.GetNoLimiter("testing-file-uploads"));
        options.AddPolicy(RateLimitPolicyNames.PaymentCallbacks, _ =>
            RateLimitPartition.GetNoLimiter("testing-payment-callbacks"));
        return;
    }

    // Global limiter is configurable so load-test environments can lift the
    // ceiling temporarily. In Production we *force* it on regardless of the
    // setting — never run prod without a global cap.
    var globalLimiterDisabled = !builder.Environment.IsProduction()
        && builder.Configuration.GetValue<bool>("RateLimiter:DisableGlobal");
    var globalLimiterPermitsPerSecond = builder.Configuration
        .GetValue<int?>("RateLimiter:GlobalPermitsPerSecond") ?? 200;
    var publicReadPermitsPerSecond = builder.Configuration
        .GetValue<int?>("RateLimiter:PublicReadPermitsPerSecond") ?? 250;

    if (!globalLimiterDisabled)
    {
        // Global limiter shields every endpoint that doesn't opt into a stricter
        // policy. Token bucket gives bursty traffic some headroom while capping
        // sustained abuse from a single user / IP.
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var path = httpContext.Request.Path.Value ?? string.Empty;
            // SignalR negotiate / hub traffic is long-lived and sensitive to
            // throttling — let the hub-level back-pressure handle it instead.
            if (path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase))
            {
                return RateLimitPartition.GetNoLimiter("unbounded");
            }

            return RateLimitPartition.GetTokenBucketLimiter(
                ResolveRateLimitKey(httpContext),
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = IsPublicCacheableRead(httpContext)
                        ? publicReadPermitsPerSecond
                        : globalLimiterPermitsPerSecond,
                    TokensPerPeriod = IsPublicCacheableRead(httpContext)
                        ? publicReadPermitsPerSecond
                        : globalLimiterPermitsPerSecond,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                    AutoReplenishment = true,
                    QueueLimit = 0
                });
        });
    }

    options.AddPolicy(RateLimitPolicyNames.Auth, httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            ResolveRateLimitKey(httpContext),
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 12,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy(RateLimitPolicyNames.FileUploads, httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            ResolveRateLimitKey(httpContext),
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(10),
                SegmentsPerWindow = 10,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy(RateLimitPolicyNames.PaymentCallbacks, httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            ResolveRateLimitKey(httpContext),
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

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
        if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        {
            policy.SetIsOriginAllowed(origin => IsAllowedDevelopmentOrigin(origin, allowedOrigins))
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
            return;
        }

        // Production: only allow non-loopback HTTPS origins from configuration.
        // localhost / 127.0.0.1 entries in production config are filtered out
        // because they widen the surface without serving any real client.
        var productionOrigins = (allowedOrigins ?? Array.Empty<string>())
            .Where(IsProductionAllowedOrigin)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (productionOrigins.Length > 0)
        {
            policy.WithOrigins(productionOrigins)
                // SignalR negotiate / long-poll sends X-Requested-With; dev uses AllowAnyHeader().
                .WithHeaders(
                    "Authorization",
                    "Content-Type",
                    "Accept",
                    "Accept-Language",
                    "Cache-Control",
                    // Settlement-processing settings use optimistic concurrency.
                    // Without this header the production CORS preflight rejects
                    // the save request and Angular reports a misleading network
                    // error instead of a normal API response.
                    "If-Match",
                    "X-Requested-With",
                    "X-SignalR-User-Agent",
                    "X-Device-Id",
                    "X-Seeding-Key",
                    "X-Moyasar-Signature",
                    "X-BankTransfer-Secret",
                    "X-Forwarded-For",
                    "X-XSRF-TOKEN",
                    RegistrationUploadTokenService.HeaderName)
                .WithExposedHeaders("ETag")
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        // Tighten clock skew (default is 5 minutes) so revoked / expired tokens
        // stop being accepted shortly after expiry. 30s allows clients with mild
        // clock drift to still succeed.
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (realtimeAllowQueryStringAccessTokens)
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    (path.StartsWithSegments(CustomerPresenceHub.HubRoute) ||
                     path.StartsWithSegments(NotificationHub.HubRoute) ||
                     path.StartsWithSegments(OrderTrackingHub.HubRoute)))
                {
                    context.Token = accessToken;
                }
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

builder.Services.AddControllers(options =>
    {
        options.Conventions.Add(new AccessAuthorizationConvention());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new SaudiDateTimeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new SaudiDateTimeOffsetJsonConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

var signalRBuilder = builder.Services.AddSignalR(o =>
{
    // Tighter limits prevent slow / abusive clients from exhausting server
    // resources under load. Send pings often enough to leave a safe margin
    // below the common 30-second mobile SignalR server timeout, including
    // normal reverse-proxy and mobile-network jitter.
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
    o.MaximumReceiveMessageSize = 32 * 1024; // 32 KB
    o.StreamBufferCapacity = 10;
    o.KeepAliveInterval = TimeSpan.FromSeconds(10);
    o.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    o.HandshakeTimeout = TimeSpan.FromSeconds(15);
});

signalRBuilder.AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.PayloadSerializerOptions.Converters.Add(new SaudiDateTimeJsonConverter());
    options.PayloadSerializerOptions.Converters.Add(new SaudiDateTimeOffsetJsonConverter());
});

// Wire the Redis backplane so SignalR scales out across multiple API
// instances without users missing notifications. Without this, hubs only
// broadcast to clients connected to the same process.
if (useRedisCaching)
{
    signalRBuilder
        .AddStackExchangeRedis(redisConnectionString!, options =>
        {
            options.ConnectionFactory = sharedRedisConnection!.GetConnectionAsync;
            options.Configuration.ChannelPrefix =
                StackExchange.Redis.RedisChannel.Literal($"{cachingSettings.Redis.InstanceName}:signalr");
        });
}

// Response compression (Brotli + Gzip) cuts bandwidth on JSON / SignalR
// long-poll payloads by 60-80%. Safe with HTTPS because EnableForHttps is
// scoped to API responses (no static asset secrets).
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
        .Concat(new[] { "application/json", "application/problem+json", "text/event-stream" });
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(
    o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(
    o => o.Level = System.IO.Compression.CompressionLevel.Fastest);

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
builder.Services.AddHealthChecks()
    .AddCheck<RedisDistributedCacheHealthCheck>(
        "redis-cache",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready", "cache"]);

// OpenTelemetry: traces + metrics for ASP.NET Core, HttpClient, EF Core,
// Redis and the .NET runtime. Metrics are exposed at /metrics in Prometheus
// format so an external scraper (Prometheus / Grafana Cloud / Azure Monitor)
// can pull them. Tracing exporter is OTLP — set OTEL_EXPORTER_OTLP_ENDPOINT
// to enable it; otherwise traces are simply collected in-process.
var otelServiceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "zadana-api";
var otelServiceNamespace = builder.Configuration["OpenTelemetry:ServiceNamespace"] ?? "zadana";
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"]
    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: otelServiceName, serviceNamespace: otelServiceNamespace,
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0")
        .AddAttributes(new[]
        {
            new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName)
        }))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(o =>
            {
                // Don't trace the noisy infrastructure endpoints — they
                // would dominate the trace budget without adding signal.
                o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health")
                                  && !ctx.Request.Path.StartsWithSegments("/metrics");
            })
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation(o =>
            {
                o.SetDbStatementForText = builder.Environment.IsDevelopment();
                o.SetDbStatementForStoredProcedure = false;
            });

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
    });

var searchableHashKeyBase64 = builder.Configuration["Security:SearchableHashKey"];
byte[] searchableHashKey;
if (!string.IsNullOrWhiteSpace(searchableHashKeyBase64))
{
    searchableHashKey = Convert.FromBase64String(searchableHashKeyBase64);
}
else if (builder.Environment.IsProduction())
{
    throw new InvalidOperationException(
        "Production startup blocked: Security:SearchableHashKey must be configured (32-byte base64 value).");
}
else
{
    searchableHashKey = System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(jwtSecret));
}

ApplicationDbContext.PiiEncryptionMasterKey = searchableHashKey;
Zadana.Domain.Modules.Identity.Services.SearchableHashProvider.Configure(searchableHashKey);

var app = builder.Build();

// Wire DataProtection into the pooled DbContext model so PII columns are
// able to read legacy enc:v1 values. New values use stable enc:v2 encryption.
ApplicationDbContext.AmbientDataProtectionProvider =
    app.Services.GetService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();

var shouldSeedOnStartup = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Seeding:EnableOnStartup");
var shouldResetOnStartup = app.Configuration.GetValue<bool>("Seeding:ResetOnStartup");
var allowRemoteSeedEndpoints = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Seeding:EnableRemoteEndpointsOnNonDevelopment");
var seedingManagementKey = app.Configuration["Seeding:ManagementKey"];

if (!app.Environment.IsEnvironment("Testing"))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
        await initialiser.InitialiseAsync();
    }
    catch (Exception ex)
    {
        LogStartupExceptionSafely(app.Services, ex, "An error occurred during database migration.");
        throw;
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Response compression breaks SignalR WebSocket / SSE / long-poll transports on
// IIS (runasp.net). Strip Accept-Encoding for hub traffic before compression runs.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
    {
        context.Request.Headers.AcceptEncoding = string.Empty;
        context.Response.Headers.CacheControl = "no-cache, no-store";
    }

    await next(context);
});

// Response compression must run early so the compression provider can wrap
// the response stream before downstream middleware writes to it.
app.UseResponseCompression();

// Forwarded headers must run before any middleware that inspects scheme / IP
// (CORS, rate limiter, redirection). Without this, X-Forwarded-* are ignored
// when the API runs behind IIS / reverse proxies / Azure App Service.
app.UseForwardedHeaders();

// HTTPS hardening for non-development environments. UseHsts is a no-op in dev.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Security headers on every response (no-op for already-set headers).
app.UseMiddleware<SecurityHeadersMiddleware>();

// Swagger is only exposed outside Production to reduce reconnaissance surface.
// Set Swagger:EnableInProduction=true to override (e.g., for staging-like envs).
var enableSwaggerInProduction = app.Configuration.GetValue<bool>("Swagger:EnableInProduction");
if (!app.Environment.IsProduction() || enableSwaggerInProduction)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

var supportedCultures = new[] { "en", "ar" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ar")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

localizationOptions.RequestCultureProviders =
[
    new AcceptLanguageHeaderRequestCultureProvider()
];

app.UseRequestLocalization(localizationOptions);
app.UseCors("Frontend");

// Authentication must run before the rate limiter so that authenticated users
// are partitioned by userId (instead of all sharing the IP-based bucket).
app.UseAuthentication();

// JWT revocation check: rejects tokens that were explicitly revoked or
// implicitly invalidated by an admin / refresh-token-reuse-detection event.
app.UseMiddleware<JwtRevocationMiddleware>();

app.UseRateLimiter();

// Allow reverse proxies/CDNs to cache only immutable public GET responses.
// Personalized and operational endpoints never receive these headers.
app.Use(async (context, next) =>
{
    if (IsPublicCacheableRead(context))
    {
        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode == StatusCodes.Status200OK &&
                !context.Response.Headers.ContainsKey("Set-Cookie"))
            {
                var edgeTtl = ResolvePublicEdgeCacheSeconds(context.Request.Path);
                context.Response.Headers.CacheControl =
                    $"public, max-age=30, s-maxage={edgeTtl}, stale-while-revalidate=30, stale-if-error=300";
                context.Response.Headers.Append("Vary", "Accept-Language, Accept-Encoding");
                context.Response.Headers["X-Zadana-Edge-Cache"] = "eligible";
            }

            return Task.CompletedTask;
        });
    }

    await next(context);
});

app.UseOutputCache();
app.UseMiddleware<TemporaryPasswordMiddleware>();
app.UseAuthorization();
app.UseMiddleware<SystemLogMiddleware>();
app.MapControllers();

var realtimeTransports = RealtimeTransportConfiguration.Resolve(
    realtimeWebSocketsEnabled,
    realtimeServerSentEventsEnabled);

void ConfigureRealtimeTransport(HttpConnectionDispatcherOptions options)
{
    // Long polling remains enabled as the universal mobile-safe fallback.
    // Keep each poll below common 30-second shared-hosting proxy timeouts.
    options.Transports = realtimeTransports;
    options.LongPolling.PollTimeout = TimeSpan.FromSeconds(25);
    options.TransportSendTimeout = TimeSpan.FromSeconds(15);
    options.ApplicationMaxBufferSize = 64 * 1024;
    options.TransportMaxBufferSize = 64 * 1024;
}

app.Logger.LogInformation(
    "SignalR transports enabled: WebSockets={WebSocketsEnabled}, ServerSentEvents={ServerSentEventsEnabled}, LongPolling=true.",
    realtimeWebSocketsEnabled,
    realtimeServerSentEventsEnabled);

app.MapHub<CustomerPresenceHub>(CustomerPresenceHub.HubRoute, ConfigureRealtimeTransport);
app.MapHub<NotificationHub>(NotificationHub.HubRoute, ConfigureRealtimeTransport);
app.MapHub<OrderTrackingHub>(OrderTrackingHub.HubRoute, ConfigureRealtimeTransport);

if (shouldSeedOnStartup)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
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
        LogStartupExceptionSafely(app.Services, ex, "An error occurred during database initialization.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapPost("/dev/reset-seed", async (
            HttpContext httpContext,
            ApplicationDbContextInitialiser initialiser,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            await initialiser.InitialiseAsync();
            var summary = await initialiser.ResetAndSeedAsync();

            logger.LogInformation("Development database reset completed successfully. Only the Super Admin account was seeded.");

            return Results.Ok(new
            {
                message = Zadana.Api.Localization.ApiLocalizedMessages.Resolve(httpContext, "DEV_DATABASE_RESET_SUCCESS"),
                summary
            });
        })
        .WithTags("Development")
        .WithSummary("Reset the development database")
        .WithDescription("Deletes development data and recreates only the Super Admin account. Available only in Development.");
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

            logger.LogInformation("Admin seed operation completed successfully via remote management endpoint.");

            return Results.Ok(new
            {
                message = Zadana.Api.Localization.ApiLocalizedMessages.Resolve(httpContext, "ADMIN_SEED_SUCCESS")
            });
        })
        .WithTags("Operations")
        .WithSummary("Ensure the Super Admin account on the current environment")
        .WithDescription("Runs the minimal admin seed logic on the current environment. Requires X-Seeding-Key.");

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

            logger.LogInformation("Reset and admin seed operation completed successfully via remote management endpoint.");

            return Results.Ok(new
            {
                message = Zadana.Api.Localization.ApiLocalizedMessages.Resolve(httpContext, "ADMIN_SEED_RESET_SUCCESS"),
                summary
            });
        })
        .WithTags("Operations")
        .WithSummary("Reset data on the current environment")
        .WithDescription("Resets the database and recreates only the Super Admin account. Requires X-Seeding-Key.");
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = WriteHealthResponseAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync
});

// Prometheus scraping endpoint. Default route is /metrics. In production,
// restrict access via reverse-proxy IP allow-list (Prometheus only).
app.MapPrometheusScrapingEndpoint();

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

    var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
    var providedBytes = Encoding.UTF8.GetBytes(providedKey.ToString());
    return expectedBytes.Length == providedBytes.Length
        && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
}

static bool IsAllowedDevelopmentOrigin(string? origin, string[]? configuredOrigins)
{
    if (string.IsNullOrWhiteSpace(origin))
    {
        return false;
    }

    if (configuredOrigins is { Length: > 0 } &&
        configuredOrigins.Any(allowedOrigin => string.Equals(allowedOrigin, origin, StringComparison.OrdinalIgnoreCase)))
    {
        return true;
    }

    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
           uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
           uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase);
}

// Filters loopback and non-https entries from a configured origin list.
// Used to defensively scrub localhost/dev origins out of Production CORS even
// if they were left in appsettings by mistake.
static bool IsProductionAllowedOrigin(string? origin)
{
    if (string.IsNullOrWhiteSpace(origin))
    {
        return false;
    }

    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return true;
}

static string ResolveRateLimitKey(HttpContext context)
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }
    }

    // After UseForwardedHeaders, RemoteIpAddress reflects the real client IP
    // when running behind a trusted proxy. We no longer trust the raw
    // X-Forwarded-For header here because it can be spoofed by clients.
    var remoteIp = context.Connection.RemoteIpAddress?.ToString();
    if (!string.IsNullOrWhiteSpace(remoteIp))
    {
        return $"ip:{remoteIp}";
    }

    return "ip:unknown";
}

static bool IsPublicCacheableRead(HttpContext context)
{
    if (!HttpMethods.IsGet(context.Request.Method) ||
        context.Request.Headers.ContainsKey("Authorization"))
    {
        return false;
    }

    var path = context.Request.Path;
    return path.StartsWithSegments("/api/home", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/api/brands", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/api/categories", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/api/geography", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/api/products", StringComparison.OrdinalIgnoreCase);
}

static int ResolvePublicEdgeCacheSeconds(PathString path)
{
    if (path.StartsWithSegments("/api/geography", StringComparison.OrdinalIgnoreCase))
    {
        return 86400;
    }

    if (path.StartsWithSegments("/api/brands", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/categories", StringComparison.OrdinalIgnoreCase))
    {
        return 1800;
    }

    return 120;
}

static void LogStartupExceptionSafely(IServiceProvider services, Exception exception, string message)
{
    try
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, message);
    }
    catch (Exception loggingException)
    {
        Console.Error.WriteLine($"{DateTime.UtcNow:o} {message}");
        Console.Error.WriteLine(exception);
        Console.Error.WriteLine("Startup exception logging fallback activated.");
        Console.Error.WriteLine(loggingException);
    }
}

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    return context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        timestamp = DateTime.UtcNow,
        checks = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => new
            {
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration_ms = entry.Value.Duration.TotalMilliseconds,
                tags = entry.Value.Tags
            })
    }));
}

// Trigger reload for watch
public partial class Program { }
