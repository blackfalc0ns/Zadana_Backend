using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.BackgroundJobs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Api.Middleware;

public sealed class SystemLogMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "temporaryPassword",
        "currentPassword",
        "newPassword",
        "confirmPassword",
        "token",
        "accessToken",
        "refreshToken",
        "authorization",
        "otp",
        "code",
        "secret",
        "apiKey",
        "iban",
        "accountIdentifier",
        "accountNumber",
        "nationalId",
        "idNumber",
        "taxId",
        "licenseNumber",
        "vehicleLicenseNumber",
        "commercialRegistrationNumber",
        "commercialRegisterDocumentUrl",
        "taxDocumentUrl",
        "licenseDocumentUrl",
        "nationalIdFrontImageUrl",
        "nationalIdBackImageUrl",
        "licenseImageUrl",
        "vehicleImageUrl",
        "personalPhotoUrl"
    };

    private const int MaxLoggedBodyCharacters = 16000;
    private readonly RequestDelegate _next;
    private readonly ISystemLogQueue _queue;
    private readonly ILogger<SystemLogMiddleware> _logger;

    public SystemLogMiddleware(
        RequestDelegate next,
        ISystemLogQueue queue,
        ILogger<SystemLogMiddleware> logger)
    {
        _next = next;
        _queue = queue;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldLog(context.Request))
        {
            await _next(context);
            return;
        }

        var request = context.Request;
        var requestBody = await TryReadAndSanitizeRequestBodyAsync(request, context.RequestAborted);
        Exception? capturedException = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            capturedException = ex;
            throw;
        }
        finally
        {
            try
            {
                EnqueueLog(context, requestBody, capturedException);
            }
            catch (Exception logException)
            {
                _logger.LogWarning(logException, "Failed to enqueue system log entry for {Method} {Path}", request.Method, request.Path);
            }
        }
    }

    private void EnqueueLog(
        HttpContext context,
        string? requestBody,
        Exception? capturedException)
    {
        var request = context.Request;
        var sourceApp = ResolveSourceApp(request.Path, context.User);
        var module = ResolveModule(request.Path);
        var targetEntityId = ResolveTargetEntityId(request.Path);
        var targetEntityType = ResolveTargetEntityType(module);
        var statusCode = capturedException is not null && context.Response.StatusCode < StatusCodes.Status400BadRequest
            ? StatusCodes.Status500InternalServerError
            : context.Response.StatusCode <= 0
                ? StatusCodes.Status500InternalServerError
                : context.Response.StatusCode;
        var isSuccess = capturedException is null && statusCode is >= 200 and < 400;
        var actorUserId = TryGetGuidClaim(context.User, ClaimTypes.NameIdentifier);
        var actorFullName = context.User.FindFirstValue(ClaimTypes.Name);
        var actorEmail = context.User.FindFirstValue(ClaimTypes.Email);
        var actorRole = context.User.FindFirstValue(ClaimTypes.Role);
        var action = BuildAction(request.Method, sourceApp, module);
        var summary = BuildSummary(request.Method, module, sourceApp, statusCode, targetEntityId);
        var metadataJson = BuildMetadataJson(context, module);

        var entry = new SystemLogEntry(
            sourceApp: sourceApp,
            module: module,
            action: action,
            summary: summary,
            requestPath: request.Path.Value ?? "/",
            httpMethod: request.Method,
            statusCode: statusCode,
            isSuccess: isSuccess,
            actorUserId: actorUserId,
            actorFullName: actorFullName,
            actorEmail: actorEmail,
            actorRole: actorRole,
            targetEntityType: targetEntityType,
            targetEntityId: targetEntityId,
            correlationId: context.TraceIdentifier,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: request.Headers.UserAgent.ToString(),
            queryString: string.IsNullOrWhiteSpace(request.QueryString.Value) ? null : request.QueryString.Value,
            requestPayloadJson: requestBody,
            metadataJson: metadataJson,
            errorMessage: capturedException?.Message);

        // Bounded queue with DropOldest — under sustained overload we lose
        // the oldest pending entries instead of blocking the request thread.
        _queue.TryEnqueue(entry);
    }

    private static bool ShouldLog(HttpRequest request)
    {
        if (!request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
        {
            return false;
        }

        var path = request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api/admin/system/logs", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string ResolveSourceApp(PathString path, ClaimsPrincipal user)
    {
        var rawPath = path.Value ?? string.Empty;
        if (rawPath.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase))
        {
            return "super_admin_panel";
        }

        if (rawPath.StartsWith("/api/vendor", StringComparison.OrdinalIgnoreCase))
        {
            return "vendor_panel";
        }

        if (rawPath.StartsWith("/api/driver", StringComparison.OrdinalIgnoreCase))
        {
            return "driver_app";
        }

        if (rawPath.StartsWith("/api/customer", StringComparison.OrdinalIgnoreCase))
        {
            return "customer_app";
        }

        var role = user.FindFirstValue(ClaimTypes.Role);
        return role switch
        {
            "SuperAdmin" or "Admin" => "super_admin_panel",
            "Vendor" or "VendorStaff" => "vendor_panel",
            "Driver" => "driver_app",
            "Customer" => "customer_app",
            _ => "public_api"
        };
    }

    private static string ResolveModule(PathString path)
    {
        var segments = (path.Value ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
        {
            return "system";
        }

        var index = segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        if (index < segments.Length &&
            (segments[index].Equals("admin", StringComparison.OrdinalIgnoreCase) ||
             segments[index].Equals("vendor", StringComparison.OrdinalIgnoreCase) ||
             segments[index].Equals("driver", StringComparison.OrdinalIgnoreCase) ||
             segments[index].Equals("customer", StringComparison.OrdinalIgnoreCase)))
        {
            index++;
        }

        if (index >= segments.Length)
        {
            return "system";
        }

        var candidate = segments[index];
        return candidate.ToLowerInvariant() switch
        {
            "auth" => "identity",
            "access" => "identity",
            "cart" => "orders",
            "checkout" => "orders",
            _ => candidate.ToLowerInvariant()
        };
    }

    private static string BuildAction(string method, string sourceApp, string module)
    {
        var normalizedMethod = method.ToUpperInvariant() switch
        {
            "POST" => "create",
            "PUT" => "update",
            "PATCH" => "update",
            "DELETE" => "delete",
            _ => method.ToLowerInvariant()
        };

        return $"{sourceApp}.{module}.{normalizedMethod}";
    }

    private static string BuildSummary(string method, string module, string sourceApp, int statusCode, string? targetEntityId)
    {
        var operation = method.ToUpperInvariant() switch
        {
            "POST" => "Created or triggered",
            "PUT" or "PATCH" => "Updated",
            "DELETE" => "Deleted",
            _ => "Processed"
        };

        var tail = string.IsNullOrWhiteSpace(targetEntityId) ? string.Empty : $" target {targetEntityId}";
        return $"{operation} {module} via {sourceApp}{tail} ({statusCode}).";
    }

    private static string? ResolveTargetEntityId(PathString path)
    {
        var segments = (path.Value ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = segments.Length - 1; i >= 0; i--)
        {
            var segment = segments[i];
            if (Guid.TryParse(segment, out _))
            {
                return segment;
            }

            if (long.TryParse(segment, out _))
            {
                return segment;
            }
        }

        return null;
    }

    private static string? ResolveTargetEntityType(string module) =>
        module switch
        {
            "orders" => "order",
            "vendors" => "vendor",
            "customers" => "customer",
            "drivers" => "driver",
            "products" or "catalog" => "catalog_item",
            "marketing" => "marketing_asset",
            "notifications" => "notification",
            "access" or "identity" => "user",
            _ => null
        };

    private static string BuildMetadataJson(HttpContext context, string module)
    {
        var request = context.Request;
        var metadata = new
        {
            module,
            route = context.GetEndpoint()?.DisplayName,
            request.ContentType,
            request.ContentLength,
            request.Host.Value
        };

        return JsonSerializer.Serialize(metadata, JsonOptions);
    }

    private static Guid? TryGetGuidClaim(ClaimsPrincipal user, string claimType)
    {
        var raw = user.FindFirstValue(claimType);
        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static async Task<string?> TryReadAndSanitizeRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength is null or <= 0)
        {
            return null;
        }

        if (request.ContentType is null ||
            !request.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
            request.ContentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        request.EnableBuffering();
        request.Body.Position = 0;

        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);

        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return null;
        }

        if (rawBody.Length > MaxLoggedBodyCharacters)
        {
            rawBody = rawBody[..MaxLoggedBodyCharacters];
        }

        try
        {
            var node = JsonNode.Parse(rawBody);
            if (node is null)
            {
                return rawBody;
            }

            SanitizeNode(node);
            return node.ToJsonString(JsonOptions);
        }
        catch
        {
            return rawBody;
        }
    }

    private static void SanitizeNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (property.Key is not null && IsSensitiveKey(property.Key))
                {
                    obj[property.Key] = "***";
                    continue;
                }

                if (property.Value is not null)
                {
                    SanitizeNode(property.Value);
                }
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                {
                    SanitizeNode(item);
                }
            }
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        if (SensitiveKeys.Contains(key))
        {
            return true;
        }

        return key.Contains("iban", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("nationalId", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("idNumber", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("taxId", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("licenseNumber", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("accountIdentifier", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("documentUrl", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("imageUrl", StringComparison.OrdinalIgnoreCase);
    }
}
